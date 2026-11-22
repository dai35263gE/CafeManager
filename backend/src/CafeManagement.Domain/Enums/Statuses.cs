namespace CafeManagement.Domain.Enums;

public enum TableStatus { Available, InUse, NeedsCleaning, Disabled }
public enum MenuItemStatus { Available, OutOfStock, Discontinued }
public enum OrderType { DineIn, TakeAway }
public enum OrderStatus { Open, Confirmed, Preparing, Served, Paid, Cancelled }
public enum OrderItemStatus { Waiting, Preparing, Completed, Served, Cancelled }
public enum PaymentMethod { Cash, BankTransfer, Card, EWallet }
public enum PromotionType { Percentage, FixedAmount }
public enum PromotionStatus { Active, Inactive, Expired }
public enum EmployeeStatus { Working, Resigned }
public enum InvoiceStatus { Paid, Cancelled }
public enum InventoryTransactionType { Import, Export, Stocktake, Adjust }
public enum AccountStatus { Active, Locked, Disabled }
