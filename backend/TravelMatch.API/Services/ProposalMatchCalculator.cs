using System.Text.RegularExpressions;
using TravelMatch.API.Models;

namespace TravelMatch.API.Services;

public static partial class ProposalMatchCalculator
{
    public const int BudgetWeight = 35;
    public const int DestinationWeight = 25;
    public const int TripTypeWeight = 15;
    public const int AvailabilityWeight = 10;
    public const int ExperienceWeight = 15;

    public static ProposalMatchResult Calculate(
        TripRequest tripRequest,
        Proposal proposal,
        GuideProfile? guideProfile)
    {
        ArgumentNullException.ThrowIfNull(tripRequest);
        ArgumentNullException.ThrowIfNull(proposal);

        var budgetScore = CalculateBudgetScore(tripRequest, proposal);
        var destinationScore = CalculateDestinationScore(tripRequest, guideProfile);
        var tripTypeScore = CalculateTripTypeScore(tripRequest, guideProfile);
        var availabilityScore = CalculateAvailabilityScore(tripRequest, proposal);
        var experienceScore = CalculateExperienceScore(guideProfile);

        var totalScore = budgetScore + destinationScore + tripTypeScore + availabilityScore + experienceScore;

        return new ProposalMatchResult
        {
            ProposalId = proposal.Id,
            GuideId = proposal.GuideId,
            BudgetScore = budgetScore,
            DestinationScore = destinationScore,
            TripTypeScore = tripTypeScore,
            AvailabilityScore = availabilityScore,
            ExperienceScore = experienceScore,
            TotalScore = Math.Clamp(Math.Round(totalScore, 2), 0m, 100m),
            BudgetWeight = BudgetWeight,
            DestinationWeight = DestinationWeight,
            TripTypeWeight = TripTypeWeight,
            AvailabilityWeight = AvailabilityWeight,
            ExperienceWeight = ExperienceWeight
        };
    }

    public static decimal CalculateBudgetScore(TripRequest tripRequest, Proposal proposal)
    {
        if (proposal.ProposedPrice <= 0m)
        {
            return 0m;
        }

        var budgetLimit = GetEffectiveBudgetLimit(tripRequest);
        if (budgetLimit <= 0m)
        {
            return 0m;
        }

        if (proposal.ProposedPrice <= budgetLimit)
        {
            return BudgetWeight;
        }

        var overageRatio = (proposal.ProposedPrice - budgetLimit) / budgetLimit;
        var penalty = overageRatio * BudgetWeight;

        return Math.Max(0m, BudgetWeight - penalty);
    }

    public static decimal CalculateDestinationScore(
        TripRequest tripRequest,
        GuideProfile? guideProfile)
    {
        if (string.IsNullOrWhiteSpace(tripRequest.Destination) ||
            string.IsNullOrWhiteSpace(guideProfile?.ServiceArea))
        {
            return 0m;
        }

        var destinationTokens = TokenizeLocation(tripRequest.Destination);
        var serviceAreaTokens = TokenizeLocation(guideProfile.ServiceArea);

        if (destinationTokens.Count == 0 || serviceAreaTokens.Count == 0)
        {
            return 0m;
        }

        var hasCommonToken = destinationTokens
            .Intersect(serviceAreaTokens, StringComparer.OrdinalIgnoreCase)
            .Any();

        return hasCommonToken ? DestinationWeight : 0m;
    }

    public static decimal CalculateTripTypeScore(
        TripRequest tripRequest,
        GuideProfile? guideProfile)
    {
        if (string.IsNullOrWhiteSpace(tripRequest.TripType) ||
            guideProfile is null ||
            string.IsNullOrWhiteSpace(guideProfile.SupportedTripTypes))
        {
            return 0m;
        }

        var touristTripTypes = TokenizeTripType(tripRequest.TripType);
        var guideTripTypes = TokenizeTripType(guideProfile.SupportedTripTypes);

        if (touristTripTypes.Count == 0 || guideTripTypes.Count == 0)
        {
            return 0m;
        }

        return touristTripTypes.Intersect(guideTripTypes, StringComparer.OrdinalIgnoreCase).Any()
            ? TripTypeWeight
            : 0m;
    }

    public static decimal CalculateAvailabilityScore(
        TripRequest tripRequest,
        Proposal proposal)
    {
        if (string.IsNullOrWhiteSpace(proposal.Availability))
        {
            return 0m;
        }

        var normalizedAvailability = NormalizeText(proposal.Availability);
        if (string.IsNullOrWhiteSpace(normalizedAvailability))
        {
            return 0m;
        }

        var containsExplicitNegation = Regex.IsMatch(
            normalizedAvailability,
            @"\b(not available|unavailable|not open|cannot make it|unable to attend)\b",
            RegexOptions.IgnoreCase);

        if (containsExplicitNegation)
        {
            return 0m;
        }

        var availabilityDates = ExtractSupportedDates(proposal.Availability);
        if (availabilityDates.Count == 0)
        {
            return 0m;
        }

        var tripStart = tripRequest.StartDate;
        var tripEnd = tripRequest.EndDate;

        var hasMatchingTripDate = availabilityDates.Any(date =>
            date >= tripStart && date <= tripEnd);

        if (hasMatchingTripDate)
        {
            return AvailabilityWeight;
        }

        return 0m;
    }

    public static decimal CalculateExperienceScore(GuideProfile? guideProfile)
    {
        if (guideProfile is null)
        {
            return 0m;
        }

        var ratingValue = guideProfile.AverageRating is null
            ? 0m
            : Math.Clamp(guideProfile.AverageRating.Value, 0m, 5m);

        var ratingScore = (ratingValue / 5m) * 8m;

        var experienceScore = guideProfile.ExperienceYears <= 0
            ? 0m
            : (Math.Min(guideProfile.ExperienceYears, 10) / 10m) * 7m;

        return Math.Min(ExperienceWeight, ratingScore + experienceScore);
    }

    private static decimal GetEffectiveBudgetLimit(TripRequest tripRequest)
    {
        var positiveValues = new[]
        {
            tripRequest.Budget,
            tripRequest.BudgetMin,
            tripRequest.BudgetMax
        }
        .Where(value => value > 0m)
        .ToArray();

        if (positiveValues.Length == 0)
        {
            return 0m;
        }

        var hasValidRange = tripRequest.BudgetMin > 0m &&
                            tripRequest.BudgetMax > 0m &&
                            tripRequest.BudgetMin <= tripRequest.BudgetMax;

        if (hasValidRange)
        {
            return tripRequest.BudgetMax;
        }

        return positiveValues.Max();
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutPunctuation = Regex.Replace(value.Trim(), @"[^\p{L}\p{N}]+", " ");
        return Regex.Replace(withoutPunctuation, @"\s+", " ")
            .ToLowerInvariant()
            .Trim();
    }

    private static List<string> TokenizeLocation(string value)
    {
        var normalized = NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> TokenizeTripType(string value)
    {
        var normalized = NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Extracts dates from free-text availability strings.
    /// <para>Supported formats (disambiguated by delimiter):</para>
    /// <list type="bullet">
    ///   <item><c>yyyy-MM-dd</c> — ISO 8601 (preferred, unambiguous)</item>
    ///   <item><c>dd MMM yyyy</c> — e.g. "10 Nov 2026"</item>
    ///   <item><c>yyyy/MM/dd</c> — e.g. "2026/11/10"</item>
    ///   <item><c>MM/dd/yyyy</c> — slash-delimited, US convention (e.g. "11/10/2026" = Nov 10)</item>
    ///   <item><c>dd-MM-yyyy</c> — dash-delimited, day-first convention (e.g. "10-11-2026" = Nov 10)</item>
    /// </list>
    /// <para>
    /// Slash vs. dash delimiter determines which format applies, preventing ambiguity
    /// between MM/dd/yyyy and dd-MM-yyyy for the same digit sequence.
    /// </para>
    /// </summary>
    private static List<DateOnly> ExtractSupportedDates(string availability)
    {
        var formats = new[]
        {
            "yyyy-MM-dd",
            "dd MMM yyyy",
            "yyyy/MM/dd",
            "MM/dd/yyyy",
            "dd-MM-yyyy"
        };

        var matches = new List<DateOnly>();
        foreach (Match match in Regex.Matches(availability, @"\d{4}-\d{2}-\d{2}|\d{2} \w{3} \d{4}|\d{2}/\d{2}/\d{4}|\d{2}-\d{2}-\d{4}|\d{4}/\d{2}/\d{2}"))
        {
            foreach (var format in formats)
            {
                if (DateOnly.TryParseExact(match.Value, format, null, System.Globalization.DateTimeStyles.None, out var parsed))
                {
                    matches.Add(parsed);
                    break;
                }
            }
        }

        return matches
            .Distinct()
            .ToList();
    }
}

public sealed class ProposalMatchResult
{
    public int ProposalId { get; set; }

    public int GuideId { get; set; }

    public decimal BudgetScore { get; set; }

    public decimal DestinationScore { get; set; }

    public decimal TripTypeScore { get; set; }

    public decimal AvailabilityScore { get; set; }

    public decimal ExperienceScore { get; set; }

    public decimal TotalScore { get; set; }

    public int BudgetWeight { get; set; }

    public int DestinationWeight { get; set; }

    public int TripTypeWeight { get; set; }

    public int AvailabilityWeight { get; set; }

    public int ExperienceWeight { get; set; }
}
