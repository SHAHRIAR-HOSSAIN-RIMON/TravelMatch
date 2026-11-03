using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMatch.API.DTOs.Profile;
using TravelMatch.API.Interfaces;

namespace TravelMatch.API.Controllers;

/// <summary>
/// Self-service profile management. The profile identity always comes from the
/// validated JWT; no user ID is accepted from the request body or route.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid authentication token." });

        var result = await _profileService.GetMyProfileAsync(userId);

        if (result.Success)
            return Ok(result);

        return result.Error switch
        {
            ProfileError.UserNotFound => NotFound(result),
            ProfileError.Unauthorized => Unauthorized(result),
            _ => BadRequest(result)
        };
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequestDto request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid authentication token." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _profileService.UpdateMyProfileAsync(userId, request);

        if (result.Success)
            return Ok(result);

        return result.Error switch
        {
            ProfileError.UserNotFound or ProfileError.ProfileNotFound => NotFound(result),
            ProfileError.Unauthorized => Unauthorized(result),
            _ => BadRequest(result)
        };
    }

    private const long MaxPhotoBytes = 5 * 1024 * 1024; // 5 MB
    public const string PhotoRequestPath = "/uploads/profile-photos";

    private static readonly Dictionary<string, string> AllowedPhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif"
    };

    /// <summary>
    /// Uploads a profile photo (JPG, PNG, WEBP or GIF, max 5 MB) for the authenticated
    /// Tourist, Guide or Organizer. The file is stored under wwwroot/uploads/profile-photos
    /// and its public URL is saved on the user's role profile.
    /// </summary>
    [HttpPost("me/photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxPhotoBytes + 64 * 1024)]
    public async Task<IActionResult> UploadMyPhoto(IFormFile? photo, [FromServices] IWebHostEnvironment env)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid authentication token." });

        if (photo is null || photo.Length == 0)
            return BadRequest(new { success = false, message = "Choose an image file to upload." });

        if (photo.Length > MaxPhotoBytes)
            return BadRequest(new { success = false, message = "The photo must be 5 MB or smaller." });

        var extension = Path.GetExtension(photo.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedPhotoTypes.ContainsKey(extension))
            return BadRequest(new { success = false, message = "Only JPG, PNG, WEBP or GIF images are allowed." });

        if (!await HasImageSignatureAsync(photo))
            return BadRequest(new { success = false, message = "The file is not a valid image." });

        var folder = GetPhotoFolder(env);
        Directory.CreateDirectory(folder);

        var fileName = $"{userId}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await photo.CopyToAsync(stream);
        }

        var (result, previousUrl) = await _profileService.SetMyPhotoAsync(userId, $"{PhotoRequestPath}/{fileName}");

        if (!result.Success)
        {
            System.IO.File.Delete(fullPath);
            return result.Error switch
            {
                ProfileError.UserNotFound or ProfileError.ProfileNotFound => NotFound(result),
                _ => BadRequest(result)
            };
        }

        DeleteOldUploadedPhoto(folder, previousUrl);

        return Ok(result);
    }

    public static string GetPhotoFolder(IWebHostEnvironment env) =>
        Path.Combine(env.ContentRootPath, "wwwroot", "uploads", "profile-photos");

    private static void DeleteOldUploadedPhoto(string folder, string? previousUrl)
    {
        // Only remove files this endpoint created; external links are left untouched.
        if (string.IsNullOrEmpty(previousUrl) || !previousUrl.StartsWith(PhotoRequestPath + "/"))
            return;

        var oldName = Path.GetFileName(previousUrl);
        var oldPath = Path.Combine(folder, oldName);

        try
        {
            if (System.IO.File.Exists(oldPath))
                System.IO.File.Delete(oldPath);
        }
        catch (IOException)
        {
            // Not critical: the new photo is already saved.
        }
    }

    /// <summary>Checks the first bytes of the file so a renamed non-image is rejected.</summary>
    private static async Task<bool> HasImageSignatureAsync(IFormFile file)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (read < 4) return false;

        bool jpg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        bool png = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
        bool gif = header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46;
        bool webp = read >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

        return jpg || png || gif || webp;
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}
