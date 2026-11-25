using Khan.Core.Catalog;

namespace Khan.Core.Persistence;

/// <summary>Starter catalog - a Khan el-Khalili shelf.</summary>
public static class SeedData
{
    public static readonly Product[] Products =
    {
        new() { Slug = "koshari-kit", Name = "Koshari Kit for 4", NameAr = "عدة كشري تكفي ٤",
                Description = "Rice, lentils, macaroni, chickpeas, crispy onions and the tomato-vinegar sauce - the whole ritual in one box.",
                Category = "Food", PriceCents = 24500, Stock = 40 },
        new() { Slug = "sinai-honey", Name = "Sinai Mountain Honey 500g", NameAr = "عسل جبلي من سيناء",
                Description = "Raw sidr honey from beekeepers in the Sinai highlands. Thick, dark, and nothing added.",
                Category = "Food", PriceCents = 39000, Stock = 25 },
        new() { Slug = "aswan-dates", Name = "Aswan Dates 1kg", NameAr = "بلح أسوان",
                Description = "Sun-dried dates from Aswan, packed the same week they come off the palm.",
                Category = "Food", PriceCents = 12000, Stock = 60 },
        new() { Slug = "karkade", Name = "Karkade (Hibiscus) 250g", NameAr = "كركديه",
                Description = "Whole dried hibiscus petals for the cold summer drink. Steep, chill, add sugar if you must.",
                Category = "Food", PriceCents = 8500, Stock = 80 },
        new() { Slug = "egyptian-cotton-tee", Name = "Egyptian Cotton T-Shirt", NameAr = "تيشيرت قطن مصري",
                Description = "Heavyweight Giza cotton, pre-shrunk, cut in Cairo. White or navy.",
                Category = "Clothing", PriceCents = 55000, Stock = 30 },
        new() { Slug = "tawla-board", Name = "Tawla (Backgammon) Board", NameAr = "طاولة زهر",
                Description = "Walnut and mother-of-pearl inlay, handmade in the Khan. Dice and stones included.",
                Category = "Crafts", PriceCents = 185000, Stock = 8 },
        new() { Slug = "kanaka", Name = "Copper Kanaka (Coffee Pot)", NameAr = "كنكة نحاس",
                Description = "Hand-hammered copper cezve for proper Turkish coffee. Gets better with every brew.",
                Category = "Crafts", PriceCents = 72000, Stock = 15 },
        new() { Slug = "papyrus-painting", Name = "Papyrus Painting - Nile at Dusk", NameAr = "لوحة بردي",
                Description = "Hand-painted on real papyrus in Giza. Ships rolled in a tube.",
                Category = "Crafts", PriceCents = 95000, Stock = 12 },
    };
}
