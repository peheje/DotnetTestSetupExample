using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

[CollectionDefinition(nameof(TestCollection))]
public class TestCollection :
    ICollectionFixture<TestCollectionSetup>,
    IClassFixture<TestClassSetup>
{
}

public class TestCollectionSetup
{
    public ServiceProvider ServiceProvider { get; }
    
    public TestCollectionSetup()
    {
        // Here you must add all necessary setup needed to build your shared DatabaseResource,
        // you can build it yourself, or register the dependencies and let your
        // dependency injection (here using Microsoft ServiceCollection) build it.
        // In this case, IDatabaseResource only needs a configuration value from appsettings.json
        
        var serviceCollection = new ServiceCollection();
        var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        serviceCollection.AddSingleton<IConfiguration>(config);
        
        // Notice transient lifetime, but it'll still be shared between tests
        // because it's the ClassFixture that calls .GetRequiredService
        serviceCollection.AddTransient<IDatabaseResource, DatabaseResource>();

        ServiceProvider = serviceCollection.BuildServiceProvider();
    }
}

public class TestClassSetup
{
    public readonly IDatabaseResource Db;   // <- This is your shared resource!

    // IClassFixture can depend on ICollectionFixture
    public TestClassSetup(TestCollectionSetup setup)
    {
        Db = setup.ServiceProvider.GetRequiredService<IDatabaseResource>();
    }
}

// Dummy database
public interface IDatabaseResource
{
    string Get();
    void Insert(string data);
}

public class DatabaseResource : IDatabaseResource
{
    private string _resource;

    // Because I have registered IConfiguration into the ServiceCollection, I can depend on it here.
    // Your database resource cannot depend on stuff that you haven't told how to build!
    public DatabaseResource(IConfiguration configuration)
    {
        if (configuration.GetConnectionString("sql-database") != "my connection string")
        {
            throw new Exception("SQL database connections string invalid");
        }
        
        // Connect to your DB, setup the DB etc. For simplicity this resource string acts as the database, initially empty
        _resource = "";
    }

    public string Get()
    {
        return _resource;
    }

    public void Insert(string data)
    {
        _resource += data;
    }
}

[Collection(nameof(TestCollection))]
public class Test
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly IDatabaseResource _db;

    public Test(TestClassSetup setup, ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        // The constructor is called before each test in this class, but since it's created by the ClassFixture
        // you'll get the same reference to the IDatabaseResource
        _db = setup.Db;
    }
    
    // One of the tests will write either Test1Test2 or Test2Test1
    
    [Fact]
    public void Test1()
    {
        _db.Insert("Test1");
        _testOutputHelper.WriteLine(_db.Get());
    }
    
    [Fact]
    public void Test2()
    {
        _db.Insert("Test2");
        _testOutputHelper.WriteLine(_db.Get());
    }
}