# GCP setup for the #4321 requeue diagnostic

This gets you from "no GCP account" to running `diagnostic/` against **real** Pub/Sub, which is
what's needed to settle #4321 (the Confirm step could not distinguish the two live hypotheses from
code alone — see `bugfix.md`). Everything here uses the `gcloud` CLI; no web console steps required.

## What you actually need

- A GCP project with the Pub/Sub API enabled.
- Credentials that resolve as [Application Default Credentials
  (ADC)](https://cloud.google.com/docs/authentication/application-default-credentials) — either
  your own user login, or a service account key.
- `roles/pubsub.editor` on that project for whichever identity you use (create/delete topics and
  subscriptions, publish, pull, ack, modack). **You do not need any Resource Manager or topic-IAM
  permissions** — this diagnostic never touches a `DeadLetterPolicy`, so it doesn't hit the
  `UpdateIAmRoleForDeadLetterAsync` path #4354 is about.
- One environment variable: `GOOGLE_CLOUD_PROJECT`.
- `PUBSUB_EMULATOR_HOST` must be **unset**, or every client in this codebase redirects to the
  emulator instead of real Pub/Sub (see `GcpPullMessageGatewayProvider.cs:58-62` for why — every
  builder opts into `EmulatorDetection.EmulatorOrProduction`).

Cost: negligible. This diagnostic sends one message and issues a few dozen Pull/ack calls — well
inside Pub/Sub's free tier. GCP does require a billing account linked to the project before it will
enable most APIs, even ones you won't be billed for at this volume — that's a GCP account
requirement, not something this diagnostic needs.

## 1. Install the gcloud CLI

If you don't already have it: <https://cloud.google.com/sdk/docs/install>. Then:

```bash
gcloud init
```

This logs you in and lets you pick or create a project interactively. If you'd rather do it by
hand, the steps below are the non-interactive equivalent.

## 2. Create a project (or pick an existing one)

```bash
gcloud auth login
gcloud projects create brighter-gcp-diag-$RANDOM --name="Brighter GCP diagnostic"
gcloud config set project brighter-gcp-diag-XXXXX   # the id gcloud just created
```

A brand-new project you create yourself makes you its **Owner**, which already includes
`pubsub.editor` — skip step 5 in that case. Only do step 5 if you're using a project you don't own,
or a separate service account.

If GCP asks you to link a billing account (it will, before it lets you enable most APIs), do that
now via `gcloud billing accounts list` + `gcloud billing projects link`, or via the console if you
don't have a billing account yet and need to create one.

## 3. Enable the Pub/Sub API

```bash
gcloud services enable pubsub.googleapis.com
```

## 4. Set up Application Default Credentials

For a personal/local run, the simplest path is your own user credentials:

```bash
gcloud auth application-default login
```

This is what `GatewayFactory.GetCredential()`
(`tests/Paramore.Brighter.Gcp.Tests/Helper/GatewayFactory.cs:13-23`) and the diagnostic script both
resolve via `GoogleCredential.GetApplicationDefault()` — no code change needed, it just picks this
up.

If you'd rather use a service account (closer to what `gcp-ci` does):

```bash
gcloud iam service-accounts create brighter-gcp-diag
gcloud iam service-accounts keys create ./gcp-diag-key.json \
  --iam-account=brighter-gcp-diag@YOUR_PROJECT_ID.iam.gserviceaccount.com
export GOOGLE_APPLICATION_CREDENTIALS=$(pwd)/gcp-diag-key.json
```

Don't commit `gcp-diag-key.json` — keep it outside the repo or add it to your global gitignore.

## 5. Grant the Pub/Sub role (only if you're not already Owner)

```bash
gcloud projects add-iam-policy-binding YOUR_PROJECT_ID \
  --member="user:you@example.com" \
  --role="roles/pubsub.editor"
```

(Use `--member="serviceAccount:brighter-gcp-diag@YOUR_PROJECT_ID.iam.gserviceaccount.com"` for the
service-account path instead.)

## 6. Set the one env var the code reads

```bash
export GOOGLE_CLOUD_PROJECT=YOUR_PROJECT_ID
unset PUBSUB_EMULATOR_HOST   # in case a previous emulator-based test run left this set
```

## 7. Sanity check

```bash
gcloud pubsub topics list --project=$GOOGLE_CLOUD_PROJECT   # should return cleanly (likely empty)
```

If that errors with a permission or API-not-enabled message, go back to step 3/5.

## 8. Run the diagnostic

See `diagnostic/README.md` (or the `Program.cs` header comment) for usage. In short:

```bash
cd bugfixes/0037-gcp-requeue-immediate-redelivery/diagnostic
dotnet run -- 10 500 60     # ackDeadlineSeconds pollTimeoutMs maxPolls — reproduces the failing shape
dotnet run -- 10 15000 4    # long polls — if this redelivers in ~1s, H1 is confirmed
dotnet run -- 60 15000 4    # long ack deadline + long polls — separates H1 from H2 per bugfix.md
```

## 9. Cleaning up afterwards

The script deletes its own topic/subscription on every run (success or failure), so there's nothing
to clean up under normal operation. If a run is killed mid-way (e.g. Ctrl+C) before its `finally`
runs:

```bash
gcloud pubsub subscriptions list --project=$GOOGLE_CLOUD_PROJECT --filter="name~fr15-diag"
gcloud pubsub topics list --project=$GOOGLE_CLOUD_PROJECT --filter="name~fr15-diag"
# then gcloud pubsub subscriptions delete / topics delete for anything left over
```

If you created a project just for this and want it gone entirely:

```bash
gcloud projects delete YOUR_PROJECT_ID
```
