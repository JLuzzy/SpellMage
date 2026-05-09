using System.Text.Json.Serialization;

namespace SamplePlugin.Models;

public class LanguageToolResponse
{
    public Match[]? matches { get; set; }

    public class Match
    {
        public int offset { get; set; }
        public int length { get; set; }
        public string? message { get; set; }
        public Replacement[]? replacements { get; set; }
        public Rule? rule { get; set; }
    }

    public class Replacement
    {
        public string? value { get; set; }
    }

    public class Rule
    {
        public string? id { get; set; }
        public string? description { get; set; }
        public Category? category { get; set; }
    }

    public class Category
    {
        public string? id { get; set; }
        public string? name { get; set; }
    }
}
