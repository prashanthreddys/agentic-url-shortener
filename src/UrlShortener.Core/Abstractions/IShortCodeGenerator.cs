namespace UrlShortener.Core.Abstractions;

/// <summary>Generates non-sequential, non-guessable short codes.</summary>
public interface IShortCodeGenerator
{
    string Generate(int length);
}
