namespace Paramore.Brighter
{
    /// <summary>
    /// What action do we take for infrastructure dependencies?
    /// -- Create: Make the required infrastructure via SDK calls
    /// -- Validate: Check if the infrastructure requested exists, and raise an error if not
    /// -- Assume: Don't check or create, assume it is there, and fail fast if not. Use to removes the cost of checks for existence on platforms where this is expensive
    /// </summary>
    public enum OnMissingChannel
    {
        Create = 0,
        Validate = 1,
        Assume = 2
    }
}
