namespace DataViz.Api.Dtos;

public record KpiDto(
    decimal Revenue,
    int OrdersCount,
    decimal AverageOrderValue,
    int UniqueCustomers);

public record SeriesPointDto(DateTime Date, decimal Revenue, int OrdersCount);

public record CategoryShareDto(string Category, decimal Revenue, int OrdersCount);

public record RegionCategoryPointDto(string Region, string Category, decimal Revenue);

public record TopProductDto(int ProductId, string Name, string Category, decimal Revenue, int Units);

public record SalesDashboardDto(
    KpiDto Kpi,
    List<SeriesPointDto> Timeseries,
    List<CategoryShareDto> CategoryShare,
    List<RegionCategoryPointDto> RegionCategory,
    List<TopProductDto> TopProducts);
