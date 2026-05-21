export type Category = {
  id: number;
  name: string;
  description: string | null;
};

export type Product = {
  id: number;
  name: string;
  description: string | null;
  price: number;
  stockQuantity: number;
  regionCode: string;
  categoryId: number;
  categoryName: string | null;
};

export type OrderItem = {
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
};

export type Order = {
  id: number;
  userId: number | null;
  userName: string | null;
  createdAt: string;
  totalPrice: number;
  regionCode: string;
  items: OrderItem[];
};

export type Kpi = {
  revenue: number;
  ordersCount: number;
  averageOrderValue: number;
  uniqueCustomers: number;
};

export type SeriesPoint = {
  date: string;
  revenue: number;
  ordersCount: number;
};

export type CategoryShare = {
  category: string;
  revenue: number;
  ordersCount: number;
};

export type RegionCategoryPoint = {
  region: string;
  category: string;
  revenue: number;
};

export type TopProduct = {
  productId: number;
  name: string;
  category: string;
  revenue: number;
  units: number;
};

export type SalesDashboard = {
  kpi: Kpi;
  timeseries: SeriesPoint[];
  categoryShare: CategoryShare[];
  regionCategory: RegionCategoryPoint[];
  topProducts: TopProduct[];
};
