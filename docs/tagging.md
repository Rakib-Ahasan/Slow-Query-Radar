# Dapper caller tagging

Reference the dependency-free `SlowQueryRadar.Tagging` project or package and tag command text before passing it to Dapper:

```csharp
var sql = "SELECT ...".WithRadarTag("Orders.Search", tenantId, "OrdersRepository.Search");
```

This produces a leading comment in the form:

```sql
/* feature:Orders.Search; tenant:tenant-42; caller:OrdersRepository.Search */
SELECT ...
```

The Collector reads only a leading tag and maps `feature`, `tenant`, and `caller` to `SlowQuerySnapshot.FeatureName`, `TenantId`, and `Caller`. Values are sanitized by the helper so they cannot terminate the comment. Untagged SQL remains supported and produces empty metadata.

Feature names can be used to group slow queries by application capability, such as `Orders.Search`, `Billing.Invoice`, or `Catalog.Lookup`.
