using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Orchestra.ApiService.Controllers;

/// <summary>
/// Provides filesystem directory browsing API for the UI folder picker.
/// </summary>
[ApiController]
[Route("v1/filesystem")]
public class FileSystemController : ControllerBase
{
    /// <summary>
    /// Gets the filesystem roots available on this system.
    /// On Windows, returns drive letters (C:\, D:\, etc.).
    /// On Unix/Linux, returns the root directory (/).
    /// </summary>
    [HttpGet("roots")]
    public ActionResult<IEnumerable<string>> GetRoots()
    {
        try
        {
            var roots = Environment.OSVersion.Platform switch
            {
                PlatformID.Win32NT => Environment.GetLogicalDrives().ToList(),
                _ => new List<string> { "/" }
            };
            return Ok(roots);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve filesystem roots.", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the immediate subdirectories of a given absolute path.
    /// </summary>
    /// <param name="path">The absolute path to list children for. Must be a valid absolute path on this system.</param>
    /// <returns>List of full paths to subdirectories.</returns>
    [HttpGet("children")]
    public ActionResult<IEnumerable<string>> GetChildren([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { error = "Path parameter is required and must not be empty." });
        }

        try
        {
            // Security: reject relative paths
            if (!Path.IsPathRooted(path))
            {
                return BadRequest(new { error = "Path must be an absolute path." });
            }

            // Security: reject path traversal attempts
            if (path.Contains(".."))
            {
                return BadRequest(new { error = "Path traversal sequences (..) are not allowed." });
            }

            // Verify the directory exists
            if (!Directory.Exists(path))
            {
                return NotFound(new { error = $"Directory not found: {path}" });
            }

            // List subdirectories
            var subdirs = Directory.GetDirectories(path)
                .OrderBy(d => Path.GetFileName(d))
                .ToList();

            return Ok(subdirs);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = $"Access denied: {path}" });
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound(new { error = $"Directory not found: {path}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to list directory.", details = ex.Message });
        }
    }

    /// <summary>
    /// Serves a local image file by absolute path. Used to render file:// image references
    /// stored in ticket comments without exposing raw file:// URLs to the browser.
    /// </summary>
    [HttpGet("image")]
    public IActionResult GetImage([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest(new { error = "Path parameter is required." });

        if (!Path.IsPathRooted(path))
            return BadRequest(new { error = "Path must be an absolute path." });

        if (path.Contains(".."))
            return BadRequest(new { error = "Path traversal sequences (..) are not allowed." });

        if (!System.IO.File.Exists(path))
            return NotFound(new { error = $"File not found: {path}" });

        var mimeType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif"            => "image/gif",
            ".svg"            => "image/svg+xml",
            ".webp"           => "image/webp",
            _                 => "image/png"
        };

        var bytes = System.IO.File.ReadAllBytes(path);
        return File(bytes, mimeType);
    }
}
