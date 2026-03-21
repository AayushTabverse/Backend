using menu_backend.Models;

namespace menu_backend.Data;

/// <summary>
/// Seeds sample data into the InMemory database for development/demo purposes.
/// Provides a complete restaurant with categories, items, tables, orders, etc.
/// </summary>
public static class SeedData
{
    public const string DemoTenantId = "demo-restaurant-001";
    public const string DemoAdminEmail = "admin@demo.com";
    public const string DemoAdminPassword = "admin123"; // plaintext for reference; stored hashed

    public static void Initialize(AppDbContext db)
    {
        if (db.Tenants.Any()) return; // Already seeded

        // ── 1. Tenant ──
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            TenantId = DemoTenantId,
            Name = "The Spice Kitchen",
            Address = "123 Food Street, Mumbai, India",
            Phone = "+91-9876543210",
            Email = DemoAdminEmail,
            LogoUrl = "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?w=200&h=200&fit=crop",
            InstagramUrl = "https://instagram.com/thespicekitchen",
            FacebookUrl = "https://facebook.com/thespicekitchen",
            TwitterUrl = "https://twitter.com/spicekitchen",
            WebsiteUrl = "https://thespicekitchen.com",
            GoogleMapsUrl = "https://maps.google.com/?q=The+Spice+Kitchen+Mumbai",
            CurrencyCode = "INR",
            IsActive = true
        };
        db.Tenants.Add(tenant);

        // ── 2. Users ──
        var admin = new User
        {
            TenantId = DemoTenantId,
            FullName = "Aayush Admin",
            Email = DemoAdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoAdminPassword),
            Role = UserRole.RestaurantAdmin,
            IsActive = true
        };

        var kitchenUser = new User
        {
            TenantId = DemoTenantId,
            FullName = "Chef Raju",
            Email = "kitchen@demo.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("kitchen123"),
            Role = UserRole.Kitchen,
            IsActive = true
        };

        var waiterUser = new User
        {
            TenantId = DemoTenantId,
            FullName = "Waiter Suresh",
            Email = "waiter@demo.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("waiter123"),
            Role = UserRole.Waiter,
            IsActive = true
        };

        db.Users.AddRange(admin, kitchenUser, waiterUser);

        // ── 3. Tables ──
        var tables = new List<RestaurantTable>();
        for (int i = 1; i <= 8; i++)
        {
            var table = new RestaurantTable
            {
                TenantId = DemoTenantId,
                TableNumber = $"T{i}",
                Label = i <= 2 ? "Window Seat" : i <= 4 ? "Main Hall" : i <= 6 ? "Garden Area" : "Private Room",
                Capacity = i <= 4 ? 4 : 6,
                IsActive = true,
                QrData = $"http://localhost:4200/menu/{DemoTenantId}/TABLE_ID_PLACEHOLDER"
            };
            tables.Add(table);
        }
        db.Tables.AddRange(tables);

        // Fix QR data with actual IDs
        foreach (var t in tables)
            t.QrData = $"http://localhost:4200/menu/{DemoTenantId}/{t.Id}";

        // ── 4. Menu Categories ──
        var catStarters = new MenuCategory
        {
            TenantId = DemoTenantId,
            Name = "Starters",
            Description = "Appetizers to kick off your meal",
            SortOrder = 1,
            IsActive = true
        };

        var catMainCourse = new MenuCategory
        {
            TenantId = DemoTenantId,
            Name = "Main Course",
            Description = "Hearty main dishes",
            SortOrder = 2,
            IsActive = true
        };

        var catBiryani = new MenuCategory
        {
            TenantId = DemoTenantId,
            Name = "Biryani & Rice",
            Description = "Fragrant rice specialties",
            SortOrder = 3,
            IsActive = true
        };

        var catBreads = new MenuCategory
        {
            TenantId = DemoTenantId,
            Name = "Breads",
            Description = "Fresh from the tandoor",
            SortOrder = 4,
            IsActive = true
        };

        var catDesserts = new MenuCategory
        {
            TenantId = DemoTenantId,
            Name = "Desserts",
            Description = "Sweet endings",
            SortOrder = 5,
            IsActive = true
        };

        var catBeverages = new MenuCategory
        {
            TenantId = DemoTenantId,
            Name = "Beverages",
            Description = "Refreshing drinks",
            SortOrder = 6,
            IsActive = true
        };

        db.MenuCategories.AddRange(catStarters, catMainCourse, catBiryani, catBreads, catDesserts, catBeverages);

        // ── 5. Menu Items ──
        var items = new List<MenuItem>
        {
            // Starters
            new() { TenantId = DemoTenantId, CategoryId = catStarters.Id, Name = "Paneer Tikka", Description = "Cottage cheese marinated in spices, grilled to perfection", Price = 249, IsVeg = true, IsAvailable = true, SortOrder = 1, PreparationTimeMinutes = 12, ImageUrl = "https://images.unsplash.com/photo-1567188040759-fb8a883dc6d8?w=400&h=300&fit=crop", ARModelUrl = "https://raw.githubusercontent.com/AayushParekh22/ar-models/main/paneer-tikka.glb" },
            new() { TenantId = DemoTenantId, CategoryId = catStarters.Id, Name = "Chicken Tikka", Description = "Tender chicken pieces marinated in yogurt & spices", Price = 299, IsVeg = false, IsAvailable = true, SortOrder = 2, PreparationTimeMinutes = 15, ImageUrl = "https://images.unsplash.com/photo-1599487488170-d11ec9c172f0?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catStarters.Id, Name = "Veg Spring Roll", Description = "Crispy rolls stuffed with vegetables", Price = 179, IsVeg = true, IsAvailable = true, SortOrder = 3, PreparationTimeMinutes = 10, ImageUrl = "https://images.unsplash.com/photo-1606525437679-037aca74a3e9?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catStarters.Id, Name = "Fish Amritsari", Description = "Battered and deep-fried fish fillets", Price = 349, IsVeg = false, IsAvailable = true, SortOrder = 4, PreparationTimeMinutes = 15, ImageUrl = "https://images.unsplash.com/photo-1544551763-46a013bb70d5?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catStarters.Id, Name = "Hara Bhara Kebab", Description = "Spinach, peas and potato patties", Price = 199, IsVeg = true, IsAvailable = true, SortOrder = 5, PreparationTimeMinutes = 10, ImageUrl = "https://images.unsplash.com/photo-1601050690597-df0568f70950?w=400&h=300&fit=crop" },

            // Main Course
            new() { TenantId = DemoTenantId, CategoryId = catMainCourse.Id, Name = "Butter Chicken", Description = "Creamy tomato-based chicken curry — our signature dish", Price = 349, IsVeg = false, IsAvailable = true, SortOrder = 1, PreparationTimeMinutes = 20, ImageUrl = "https://images.unsplash.com/photo-1603894584373-5ac82b2ae398?w=400&h=300&fit=crop", ARModelUrl = "https://raw.githubusercontent.com/AayushParekh22/ar-models/main/butter-chicken.glb" },
            new() { TenantId = DemoTenantId, CategoryId = catMainCourse.Id, Name = "Dal Makhani", Description = "Slow-cooked black lentils in buttery gravy", Price = 249, IsVeg = true, IsAvailable = true, SortOrder = 2, PreparationTimeMinutes = 18, ImageUrl = "https://images.unsplash.com/photo-1546833999-b9f581a1996d?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catMainCourse.Id, Name = "Palak Paneer", Description = "Cottage cheese cubes in creamy spinach curry", Price = 269, IsVeg = true, IsAvailable = true, SortOrder = 3, PreparationTimeMinutes = 15, ImageUrl = "https://images.unsplash.com/photo-1588166524941-3bf61a9c41db?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catMainCourse.Id, Name = "Chicken Kadai", Description = "Chicken cooked with bell peppers in kadai masala", Price = 329, IsVeg = false, IsAvailable = true, SortOrder = 4, PreparationTimeMinutes = 20, ImageUrl = "https://images.unsplash.com/photo-1565557623262-b51c2513a641?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catMainCourse.Id, Name = "Mutton Rogan Josh", Description = "Kashmiri-style slow-cooked lamb curry", Price = 449, IsVeg = false, IsAvailable = true, SortOrder = 5, PreparationTimeMinutes = 25, ImageUrl = "https://images.unsplash.com/photo-1574653853027-5382a3d23a15?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catMainCourse.Id, Name = "Shahi Paneer", Description = "Rich and creamy paneer in cashew-based gravy", Price = 289, IsVeg = true, IsAvailable = true, SortOrder = 6, PreparationTimeMinutes = 15, ImageUrl = "https://images.unsplash.com/photo-1631452180519-c014fe946bc7?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catMainCourse.Id, Name = "Chole Masala", Description = "Spiced chickpea curry — North Indian classic", Price = 219, IsVeg = true, IsAvailable = false, SortOrder = 7, PreparationTimeMinutes = 15, ImageUrl = "https://images.unsplash.com/photo-1585937421612-70a008356fbe?w=400&h=300&fit=crop" },

            // Biryani & Rice
            new() { TenantId = DemoTenantId, CategoryId = catBiryani.Id, Name = "Chicken Biryani", Description = "Fragrant basmati rice layered with spiced chicken", Price = 329, IsVeg = false, IsAvailable = true, SortOrder = 1, PreparationTimeMinutes = 25, ImageUrl = "https://images.unsplash.com/photo-1563379091339-03b21ab4a4f8?w=400&h=300&fit=crop", ARModelUrl = "https://raw.githubusercontent.com/AayushParekh22/ar-models/main/chicken-biryani.glb" },
            new() { TenantId = DemoTenantId, CategoryId = catBiryani.Id, Name = "Veg Biryani", Description = "Aromatic rice with mixed vegetables and saffron", Price = 249, IsVeg = true, IsAvailable = true, SortOrder = 2, PreparationTimeMinutes = 20, ImageUrl = "https://images.unsplash.com/photo-1589302168068-964664d93dc0?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catBiryani.Id, Name = "Mutton Biryani", Description = "Hyderabadi-style dum biryani with tender mutton", Price = 449, IsVeg = false, IsAvailable = true, SortOrder = 3, PreparationTimeMinutes = 30, ImageUrl = "https://images.unsplash.com/photo-1642821373181-696a54913e93?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catBiryani.Id, Name = "Jeera Rice", Description = "Cumin-flavored steamed basmati rice", Price = 129, IsVeg = true, IsAvailable = true, SortOrder = 4, PreparationTimeMinutes = 10, ImageUrl = "https://images.unsplash.com/photo-1516714435131-44d6b64dc6a2?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catBiryani.Id, Name = "Steamed Rice", Description = "Plain steamed basmati rice", Price = 99, IsVeg = true, IsAvailable = true, SortOrder = 5, PreparationTimeMinutes = 8, ImageUrl = "https://images.unsplash.com/photo-1536304929831-ee1ca9d44726?w=400&h=300&fit=crop" },

            // Breads
            new() { TenantId = DemoTenantId, CategoryId = catBreads.Id, Name = "Butter Naan", Description = "Soft leavened bread brushed with butter", Price = 59, IsVeg = true, IsAvailable = true, SortOrder = 1, PreparationTimeMinutes = 5, ImageUrl = "https://images.unsplash.com/photo-1565557623262-b51c2513a641?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catBreads.Id, Name = "Garlic Naan", Description = "Naan topped with garlic and cilantro", Price = 69, IsVeg = true, IsAvailable = true, SortOrder = 2, PreparationTimeMinutes = 5, ImageUrl = "https://images.unsplash.com/photo-1604908176997-125f25cc6f3d?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catBreads.Id, Name = "Cheese Naan", Description = "Naan stuffed with melted cheese", Price = 89, IsVeg = true, IsAvailable = true, SortOrder = 3, PreparationTimeMinutes = 7, ImageUrl = "https://images.unsplash.com/photo-1574071318508-1cdbab80d002?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catBreads.Id, Name = "Tandoori Roti", Description = "Whole wheat bread from the tandoor", Price = 39, IsVeg = true, IsAvailable = true, SortOrder = 4, PreparationTimeMinutes = 4, ImageUrl = "https://images.unsplash.com/photo-1586444248879-bc604cbd555a?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catBreads.Id, Name = "Laccha Paratha", Description = "Layered flaky flatbread", Price = 59, IsVeg = true, IsAvailable = true, SortOrder = 5, PreparationTimeMinutes = 6, ImageUrl = "https://images.unsplash.com/photo-1585937421612-70a008356fbe?w=400&h=300&fit=crop" },

            // Desserts
            new() { TenantId = DemoTenantId, CategoryId = catDesserts.Id, Name = "Gulab Jamun", Description = "Deep-fried milk dumplings soaked in sugar syrup (2 pcs)", Price = 119, IsVeg = true, IsAvailable = true, SortOrder = 1, PreparationTimeMinutes = 5, ImageUrl = "https://images.unsplash.com/photo-1666190050401-30b1756a5fb0?w=400&h=300&fit=crop", ARModelUrl = "https://raw.githubusercontent.com/AayushParekh22/ar-models/main/gulab-jamun.glb" },
            new() { TenantId = DemoTenantId, CategoryId = catDesserts.Id, Name = "Rasmalai", Description = "Soft cottage cheese patties in flavored milk", Price = 149, IsVeg = true, IsAvailable = true, SortOrder = 2, PreparationTimeMinutes = 5, ImageUrl = "https://images.unsplash.com/photo-1571006682205-15b3a9f42810?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catDesserts.Id, Name = "Brownie with Ice Cream", Description = "Warm chocolate brownie topped with vanilla ice cream", Price = 199, IsVeg = true, IsAvailable = true, SortOrder = 3, PreparationTimeMinutes = 8, ImageUrl = "https://images.unsplash.com/photo-1564355808539-22fda35bed7e?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catDesserts.Id, Name = "Kulfi", Description = "Traditional Indian ice cream — malai flavor", Price = 99, IsVeg = true, IsAvailable = true, SortOrder = 4, PreparationTimeMinutes = 3, ImageUrl = "https://images.unsplash.com/photo-1488900128323-21503983a07e?w=400&h=300&fit=crop" },

            // Beverages
            new() { TenantId = DemoTenantId, CategoryId = catBeverages.Id, Name = "Masala Chai", Description = "Indian spiced tea", Price = 49, IsVeg = true, IsAvailable = true, SortOrder = 1, PreparationTimeMinutes = 5, ImageUrl = "https://images.unsplash.com/photo-1597318181409-cf64d0b5d8a2?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catBeverages.Id, Name = "Cold Coffee", Description = "Chilled coffee blended with ice cream", Price = 129, IsVeg = true, IsAvailable = true, SortOrder = 2, PreparationTimeMinutes = 5, ImageUrl = "https://images.unsplash.com/photo-1461023058943-07fcbe16d735?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catBeverages.Id, Name = "Mango Lassi", Description = "Creamy yogurt drink with mango pulp", Price = 109, IsVeg = true, IsAvailable = true, SortOrder = 3, PreparationTimeMinutes = 5, ImageUrl = "https://images.unsplash.com/photo-1553530666-ba11a7da3888?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catBeverages.Id, Name = "Fresh Lime Soda", Description = "Sweet or salty — your choice", Price = 79, IsVeg = true, IsAvailable = true, SortOrder = 4, PreparationTimeMinutes = 3, ImageUrl = "https://images.unsplash.com/photo-1513558161293-cdaf765ed2fd?w=400&h=300&fit=crop" },
            new() { TenantId = DemoTenantId, CategoryId = catBeverages.Id, Name = "Buttermilk (Chaas)", Description = "Cool spiced buttermilk", Price = 59, IsVeg = true, IsAvailable = true, SortOrder = 5, PreparationTimeMinutes = 3, ImageUrl = "https://images.unsplash.com/photo-1543253687-c931c8e01820?w=400&h=300&fit=crop" },
        };

        // Add modifiers to some items
        items[0].Modifiers.Add(new MenuItemModifier { TenantId = DemoTenantId, Name = "Extra Cheese", AdditionalPrice = 40, IsAvailable = true });
        items[0].Modifiers.Add(new MenuItemModifier { TenantId = DemoTenantId, Name = "Extra Spicy", AdditionalPrice = 0, IsAvailable = true });
        items[1].Modifiers.Add(new MenuItemModifier { TenantId = DemoTenantId, Name = "Boneless", AdditionalPrice = 30, IsAvailable = true });
        items[5].Modifiers.Add(new MenuItemModifier { TenantId = DemoTenantId, Name = "Extra Gravy", AdditionalPrice = 50, IsAvailable = true });
        items[5].Modifiers.Add(new MenuItemModifier { TenantId = DemoTenantId, Name = "Less Spicy", AdditionalPrice = 0, IsAvailable = true });
        items[12].Modifiers.Add(new MenuItemModifier { TenantId = DemoTenantId, Name = "Extra Raita", AdditionalPrice = 30, IsAvailable = true });
        items[17].Modifiers.Add(new MenuItemModifier { TenantId = DemoTenantId, Name = "Extra Butter", AdditionalPrice = 10, IsAvailable = true });
        items[22].Modifiers.Add(new MenuItemModifier { TenantId = DemoTenantId, Name = "Extra Scoop", AdditionalPrice = 60, IsAvailable = true });

        db.MenuItems.AddRange(items);

        // ── 6. Sample Orders ──
        var order1 = new Order
        {
            TenantId = DemoTenantId,
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-0001",
            TableId = tables[0].Id,
            Status = OrderStatus.Preparing,
            SubTotal = 907,
            Tax = 45,
            TotalAmount = 952,
            EstimatedMinutes = 20,
            AcceptedAt = DateTime.UtcNow.AddMinutes(-10),
            CreatedAt = DateTime.UtcNow.AddMinutes(-15),
            Items = new List<OrderItem>
            {
                new() { TenantId = DemoTenantId, MenuItemId = items[0].Id, ItemName = "Paneer Tikka", Quantity = 1, UnitPrice = 249, TotalPrice = 249 },
                new() { TenantId = DemoTenantId, MenuItemId = items[5].Id, ItemName = "Butter Chicken", Quantity = 1, UnitPrice = 349, TotalPrice = 349, Notes = "Less spicy please" },
                new() { TenantId = DemoTenantId, MenuItemId = items[17].Id, ItemName = "Butter Naan", Quantity = 3, UnitPrice = 59, TotalPrice = 177 },
                new() { TenantId = DemoTenantId, MenuItemId = items[26].Id, ItemName = "Masala Chai", Quantity = 2, UnitPrice = 49, TotalPrice = 98, Modifiers = "Extra sugar" },
            }
        };

        var order2 = new Order
        {
            TenantId = DemoTenantId,
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-0002",
            TableId = tables[2].Id,
            Status = OrderStatus.Pending,
            SubTotal = 777,
            Tax = 39,
            TotalAmount = 816,
            EstimatedMinutes = 25,
            CreatedAt = DateTime.UtcNow.AddMinutes(-3),
            SpecialInstructions = "Nut allergy — please be careful",
            Items = new List<OrderItem>
            {
                new() { TenantId = DemoTenantId, MenuItemId = items[12].Id, ItemName = "Chicken Biryani", Quantity = 2, UnitPrice = 329, TotalPrice = 658, Modifiers = "Extra Raita" },
                new() { TenantId = DemoTenantId, MenuItemId = items[27].Id, ItemName = "Cold Coffee", Quantity = 1, UnitPrice = 129, TotalPrice = 129 },
            }
        };

        var order3 = new Order
        {
            TenantId = DemoTenantId,
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-0003",
            TableId = tables[4].Id,
            Status = OrderStatus.Ready,
            SubTotal = 517,
            Tax = 26,
            TotalAmount = 543,
            EstimatedMinutes = 15,
            AcceptedAt = DateTime.UtcNow.AddMinutes(-20),
            PreparedAt = DateTime.UtcNow.AddMinutes(-2),
            CreatedAt = DateTime.UtcNow.AddMinutes(-25),
            Items = new List<OrderItem>
            {
                new() { TenantId = DemoTenantId, MenuItemId = items[7].Id, ItemName = "Palak Paneer", Quantity = 1, UnitPrice = 269, TotalPrice = 269 },
                new() { TenantId = DemoTenantId, MenuItemId = items[13].Id, ItemName = "Veg Biryani", Quantity = 1, UnitPrice = 249, TotalPrice = 249 },
            }
        };

        var order4 = new Order
        {
            TenantId = DemoTenantId,
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-0004",
            TableId = tables[1].Id,
            Status = OrderStatus.Completed,
            SubTotal = 1296,
            Tax = 65,
            TotalAmount = 1361,
            EstimatedMinutes = 30,
            AcceptedAt = DateTime.UtcNow.AddHours(-2),
            PreparedAt = DateTime.UtcNow.AddHours(-1).AddMinutes(-40),
            ServedAt = DateTime.UtcNow.AddHours(-1).AddMinutes(-35),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-2).AddMinutes(-10),
            Items = new List<OrderItem>
            {
                new() { TenantId = DemoTenantId, MenuItemId = items[1].Id, ItemName = "Chicken Tikka", Quantity = 2, UnitPrice = 299, TotalPrice = 598 },
                new() { TenantId = DemoTenantId, MenuItemId = items[9].Id, ItemName = "Mutton Rogan Josh", Quantity = 1, UnitPrice = 449, TotalPrice = 449 },
                new() { TenantId = DemoTenantId, MenuItemId = items[17].Id, ItemName = "Butter Naan", Quantity = 2, UnitPrice = 59, TotalPrice = 118 },
                new() { TenantId = DemoTenantId, MenuItemId = items[28].Id, ItemName = "Mango Lassi", Quantity = 1, UnitPrice = 109, TotalPrice = 109 },
            }
        };

        var order5 = new Order
        {
            TenantId = DemoTenantId,
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-0005",
            TableId = tables[5].Id,
            Status = OrderStatus.Accepted,
            SubTotal = 568,
            Tax = 28,
            TotalAmount = 596,
            EstimatedMinutes = 20,
            AcceptedAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddMinutes(-8),
            Items = new List<OrderItem>
            {
                new() { TenantId = DemoTenantId, MenuItemId = items[6].Id, ItemName = "Dal Makhani", Quantity = 1, UnitPrice = 249, TotalPrice = 249 },
                new() { TenantId = DemoTenantId, MenuItemId = items[10].Id, ItemName = "Shahi Paneer", Quantity = 1, UnitPrice = 289, TotalPrice = 289 },
                new() { TenantId = DemoTenantId, MenuItemId = items[20].Id, ItemName = "Tandoori Roti", Quantity = 4, UnitPrice = 39, TotalPrice = 156, Notes = "Well done" },
            }
        };

        db.Orders.AddRange(order1, order2, order3, order4, order5);

        // ── 7. Print Jobs ──
        db.PrintJobs.AddRange(
            new PrintJob { TenantId = DemoTenantId, OrderId = order1.Id, Status = PrintJobStatus.Completed, PrintedAt = DateTime.UtcNow.AddMinutes(-14) },
            new PrintJob { TenantId = DemoTenantId, OrderId = order2.Id, Status = PrintJobStatus.Pending },
            new PrintJob { TenantId = DemoTenantId, OrderId = order3.Id, Status = PrintJobStatus.Completed, PrintedAt = DateTime.UtcNow.AddMinutes(-24) },
            new PrintJob { TenantId = DemoTenantId, OrderId = order5.Id, Status = PrintJobStatus.Pending }
        );

        // ── 8. Payment for completed order ──
        db.Payments.Add(new Payment
        {
            TenantId = DemoTenantId,
            OrderId = order4.Id,
            Amount = 1361,
            Method = PaymentMethod.UPI,
            Status = PaymentStatus.Completed,
            TransactionId = "UPI-TXN-" + Guid.NewGuid().ToString()[..8].ToUpper(),
            PaidAt = DateTime.UtcNow.AddHours(-1)
        });

        db.SaveChanges();
    }
}
