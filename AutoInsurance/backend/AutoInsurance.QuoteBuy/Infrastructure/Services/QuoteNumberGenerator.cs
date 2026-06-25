namespace AutoInsurance.QuoteBuy.Infrastructure.Services;

public interface IQuoteNumberGenerator
{
    string Generate();
}

public class QuoteNumberGenerator : IQuoteNumberGenerator
{
    private static readonly char[] Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    public string Generate()
    {
        var random = new Random();
        var suffix = new string(Enumerable.Range(0, 8).Select(_ => Chars[random.Next(Chars.Length)]).ToArray());
        return $"Q-{DateTime.UtcNow:yyyyMMdd}-{suffix}";
    }
}
