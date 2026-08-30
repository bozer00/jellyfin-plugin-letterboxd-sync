using Xunit;
using LetterboxdRatings.Providers;

namespace Jellybox.Tests
{
    public class RatingsParserTests
    {
        [Fact]
        public void ParseRatingFromHtml_WithTwitterMetaTag_ReturnsCorrectRating()
        {
            // Arrange
            var html = @"
                <html>
                    <head>
                        <meta name=""twitter:data2"" content=""3.22 out of 5"" />
                    </head>
                </html>
            ";
            var provider = new LetterboxdRatingProvider(null!);

            // Act
            var rating = provider.ParseRatingFromHtml(html);

            // Assert
            Assert.NotNull(rating);
            Assert.Equal(3.22f, rating.Value);
        }

        [Fact]
        public void ParseRatingFromHtml_WithTwitterMetaTagAlternativeOrder_ReturnsCorrectRating()
        {
            // Arrange
            var html = @"
                <html>
                    <head>
                        <meta content=""4.15 out of 5"" name=""twitter:data2"" />
                    </head>
                </html>
            ";
            var provider = new LetterboxdRatingProvider(null!);

            // Act
            var rating = provider.ParseRatingFromHtml(html);

            // Assert
            Assert.NotNull(rating);
            Assert.Equal(4.15f, rating.Value);
        }

        [Fact]
        public void ParseRatingFromHtml_WithJsonLdFallback_ReturnsCorrectRating()
        {
            // Arrange
            var html = @"
                <html>
                    <body>
                        <script type=""application/ld+json"">
                        {
                            ""@context"": ""http://schema.org"",
                            ""@type"": ""Movie"",
                            ""name"": ""Parasite"",
                            ""aggregateRating"": {
                                ""@type"": ""AggregateRating"",
                                ""ratingValue"": 4.56,
                                ""bestRating"": 5.0,
                                ""ratingCount"": 125432
                            }
                        }
                        </script>
                    </body>
                </html>
            ";
            var provider = new LetterboxdRatingProvider(null!);

            // Act
            var rating = provider.ParseRatingFromHtml(html);

            // Assert
            Assert.NotNull(rating);
            Assert.Equal(4.56f, rating.Value);
        }

        [Fact]
        public void ParseRatingFromHtml_WithNoRating_ReturnsNull()
        {
            // Arrange
            var html = @"
                <html>
                    <head>
                        <title>No Ratings Page</title>
                    </head>
                </html>
            ";
            var provider = new LetterboxdRatingProvider(null!);

            // Act
            var rating = provider.ParseRatingFromHtml(html);

            // Assert
            Assert.Null(rating);
        }

        [Fact]
        public void ParseRatingFromHtml_WithOutOfRangeRating_ReturnsNull()
        {
            var provider = new LetterboxdRatingProvider(null!);

            var rating = provider.ParseRatingFromHtml("<meta name=\"twitter:data2\" content=\"6.2 out of 5\">");

            Assert.Null(rating);
        }
    }
}
