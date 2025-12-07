// Quick utility to generate BCrypt hash
// Usage: dotnet script HashGenerator.cs "Admin123!"

using BCrypt.Net;

var password = args.Length > 0 ? args[0] : "Admin123!";
var hash = BCrypt.Net.BCrypt.HashPassword(password);
Console.WriteLine($"Password: {password}");
Console.WriteLine($"BCrypt Hash: {hash}");

