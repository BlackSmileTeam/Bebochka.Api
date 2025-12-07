# PowerShell script to generate BCrypt hash
# Requires: dotnet-sdk and BCrypt.Net-Next package

$code = @"
using BCrypt.Net;
using System;

class Program {
    static void Main() {
        var password = "Admin123!";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        Console.WriteLine("Password: " + password);
        Console.WriteLine("BCrypt Hash: " + hash);
    }
}
"@

# Create a temporary C# file
$tempFile = [System.IO.Path]::GetTempFileName() + ".cs"
$code | Out-File -FilePath $tempFile -Encoding UTF8

# Compile and run (simplified approach - just output a known valid hash)
# For now, let's use a pre-generated hash
Write-Host "Generating BCrypt hash for 'Admin123!'..."
Write-Host ""
Write-Host "Note: This requires running the actual C# code."
Write-Host "For now, use the registration endpoint to create the admin user."

