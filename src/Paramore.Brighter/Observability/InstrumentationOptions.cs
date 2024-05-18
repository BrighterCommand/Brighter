using System;

namespace Paramore.Brighter.Observability;

[Flags]
public enum InstrumentationOptions
{
    None = 0,
    RequestInformation = 1,                                     //(.requestid, .requestids, .requesttype, .operation) => what is the request?
    RequestBody = 2,                                            //(.requestbody) => what is the request body?
    RequestContext = 4,                                         //(.requestcontext) => what is the request context?
    All = RequestInformation | RequestBody | RequestContext     //(.requestid, .requestids, .requesttype, .operation, .requestbody, .requestcontext) => what is the whole request?
}

