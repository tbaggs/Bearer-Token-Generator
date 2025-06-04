# Security Guidelines for Future Development

## SQL Injection Prevention

If database operations are added to this application in the future, follow these guidelines to prevent SQL injection vulnerabilities:

### 1. Always Use Parameterized Queries

```csharp
// ✅ SECURE - Parameterized query
public async Task<User> GetUserAsync(int userId)
{
    using var connection = new SqlConnection(connectionString);
    using var command = new SqlCommand("SELECT * FROM Users WHERE Id = @userId", connection);
    command.Parameters.AddWithValue("@userId", userId);
    
    await connection.OpenAsync();
    using var reader = await command.ExecuteReaderAsync();
    // Process results...
}

// ❌ VULNERABLE - String concatenation
public async Task<User> GetUserAsync(int userId)
{
    var query = $"SELECT * FROM Users WHERE Id = {userId}"; // Don't do this!
    // This is vulnerable to SQL injection
}
```

### 2. Use Entity Framework Core for Complex Operations

```csharp
// ✅ SECURE - Entity Framework automatically parameterizes queries
public async Task<List<User>> GetActiveUsersAsync()
{
    return await context.Users
        .Where(u => u.IsActive)
        .ToListAsync();
}
```

### 3. Validate All User Input

```csharp
public async Task<User> GetUserAsync(string username)
{
    // Validate input first
    if (string.IsNullOrWhiteSpace(username) || username.Length > 100)
    {
        throw new ArgumentException("Invalid username");
    }
    
    // Use parameterized query
    using var command = new SqlCommand("SELECT * FROM Users WHERE Username = @username", connection);
    command.Parameters.AddWithValue("@username", username);
    // Execute query...
}
```

### 4. Use Stored Procedures with Parameters

```csharp
// ✅ SECURE - Stored procedure with parameters
public async Task<int> CreateUserAsync(string username, string email)
{
    using var command = new SqlCommand("sp_CreateUser", connection);
    command.CommandType = CommandType.StoredProcedure;
    command.Parameters.AddWithValue("@Username", username);
    command.Parameters.AddWithValue("@Email", email);
    
    return (int)await command.ExecuteScalarAsync();
}
```

### 5. Never Build Dynamic SQL from User Input

```csharp
// ❌ NEVER DO THIS
public void SearchUsers(string searchTerm, string sortColumn)
{
    // This is extremely dangerous!
    var query = $"SELECT * FROM Users WHERE Name LIKE '%{searchTerm}%' ORDER BY {sortColumn}";
}

// ✅ SECURE ALTERNATIVE
public void SearchUsers(string searchTerm, string sortColumn)
{
    // Whitelist allowed sort columns
    var allowedColumns = new[] { "Name", "Email", "Created" };
    if (!allowedColumns.Contains(sortColumn))
    {
        throw new ArgumentException("Invalid sort column");
    }
    
    // Use parameterized query for search term
    var query = $"SELECT * FROM Users WHERE Name LIKE @searchTerm ORDER BY {sortColumn}";
    // Add parameter for searchTerm...
}
```

## Additional Security Best Practices

### Connection String Security
- Store connection strings in secure configuration (Azure Key Vault, etc.)
- Use integrated authentication when possible
- Apply principle of least privilege to database accounts

### Error Handling
```csharp
try
{
    // Database operations
}
catch (SqlException ex)
{
    // ❌ Don't expose database details
    // throw new Exception($"SQL Error: {ex.Message}");
    
    // ✅ Log detailed error, return generic message
    _logger.LogError(ex, "Database error occurred");
    throw new Exception("An error occurred while processing your request");
}
```

### Input Validation
- Validate length, format, and content of all inputs
- Use strong typing and data annotations
- Sanitize inputs that will be displayed to users

### Regular Security Reviews
- Conduct code reviews focusing on security
- Use static analysis tools (SonarQube, etc.)
- Keep dependencies updated
- Perform penetration testing

---

**Note**: This application currently contains no database operations and is therefore not vulnerable to SQL injection. These guidelines apply to future development that may include database functionality.