using System;

namespace Paramore.Brighter.BindingAttributes;

/// <summary>
/// Indicates that this method is the target site for bulk operations
/// Due to the need to bind a collection of IRequest to their concrete type but
/// this information being lost without casting we have to bind the call to the per-request
/// method to the concrete type
/// </summary>
public class DepositCallSiteAttribute : Attribute;

/// Indicates that this method is the target site for bulk operations
/// Due to the need to bind a collection of IRequest to their concrete type but
/// this information being lost without casting we have to bind the call to the per-request
/// method to the concrete type
public class DepositCallSiteAsyncAttribute : Attribute;

/// Indicates that this method is the target site for invoking a bulk deposit when the
/// transaction type isn't known until runtime
public class BulkDepositCallSiteAttribute : Attribute;

/// Indicates that this method is the target site for invoking a bulk deposit when the
/// transaction type isn't known until runtime
public class BulkDepositCallSiteAsyncAttribute : Attribute;
