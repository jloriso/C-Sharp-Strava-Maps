namespace GpxWorldMap.Models
{
    /// <summary>One row from the Strava export's shoes.csv.</summary>
    public record ShoeRecord(string Name, string Brand, string Model)
    {
        /// <summary>How the activities CSV's Activity Gear column refers to this shoe
        /// ("{Brand} {Model} {Name}"), with whitespace normalized.</summary>
        public string GearLabel => NormalizeLabel($"{Brand} {Model} {Name}");

        public static string NormalizeLabel(string label) =>
            string.Join(' ', label.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}