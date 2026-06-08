using FurnitureAuthoring.Application.Services;
using FurnitureAuthoring.Contracts.Models;
using FurnitureAuthoring.Infrastructure.Persistence;

return await new TestRunner().RunAllAsync();

internal sealed class TestRunner
{
    private int failures;

    public async Task<int> RunAllAsync()
    {
        await Run("Validator reports required manifest and item fields", () =>
        {
            FurnitureManifestValidator validator = new();
            FurnitureManifestDto manifest = new()
            {
                ManifestVersion = 0,
                CatalogVersion = "",
                Author = "",
            };
            manifest.Items.Add(new FurnitureItemDto());

            IReadOnlyList<string> errors = validator.Validate(manifest);

            AssertContains(errors, "ManifestVersion must be greater than zero.");
            AssertContains(errors, "CatalogVersion is required.");
            AssertContains(errors, "Author is required.");
            AssertContains(errors, "Item[0] code is required.");
            AssertContains(errors, "Item[0] displayName is required.");
            AssertContains(errors, "Item[0] exportCode is required.");
            AssertContains(errors, "Item[0] prefabSourcePath is required.");
        });

        await Run("Validator reports duplicate furniture codes", () =>
        {
            FurnitureManifestValidator validator = new();
            FurnitureManifestDto manifest = CreateValidManifest();
            manifest.Items.Add(CreateValidItem("A001"));
            manifest.Items.Add(CreateValidItem("a001"));

            IReadOnlyList<string> errors = validator.Validate(manifest);

            AssertContains(errors, "Duplicate furniture code: a001");
        });

        await Run("Json manifest store round trips manifest", async () =>
        {
            string path = Path.Combine(Path.GetTempPath(), "FurnitureAuthoring.Tests", $"{Guid.NewGuid():N}", "manifest.json");
            JsonFurnitureManifestStore store = new();
            FurnitureManifestDto manifest = CreateValidManifest();
            manifest.Items.Add(CreateValidItem("A001"));

            await store.SaveAsync(path, manifest);
            FurnitureManifestDto loaded = await store.LoadAsync(path);

            AssertEqual("CatalogVersion", manifest.CatalogVersion, loaded.CatalogVersion);
            AssertEqual("Author", manifest.Author, loaded.Author);
            AssertEqual("Items.Count", 1, loaded.Items.Count);
            AssertEqual("Items[0].Code", "A001", loaded.Items[0].Code);
        });

        if (failures > 0)
        {
            Console.Error.WriteLine($"{failures} test(s) failed.");
            return 1;
        }

        Console.WriteLine("All FurnitureAuthoring tests passed.");
        return 0;
    }

    private async Task Run(string name, Action test)
    {
        await Run(name, () =>
        {
            test();
            return Task.CompletedTask;
        });
    }

    private async Task Run(string name, Func<Task> test)
    {
        try
        {
            await test();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception exception)
        {
            failures++;
            Console.Error.WriteLine($"FAIL {name}");
            Console.Error.WriteLine(exception.Message);
        }
    }

    private static FurnitureManifestDto CreateValidManifest()
    {
        return new FurnitureManifestDto
        {
            ManifestVersion = 1,
            CatalogVersion = "catalog-v1",
            Author = "test",
        };
    }

    private static FurnitureItemDto CreateValidItem(string code)
    {
        return new FurnitureItemDto
        {
            Code = code,
            DisplayName = $"Item {code}",
            ExportCode = $"EXPORT-{code}",
            PrefabSourcePath = $"prefabs/{code}.prefab",
        };
    }

    private static void AssertContains(IReadOnlyList<string> values, string expected)
    {
        if (!values.Contains(expected))
        {
            throw new InvalidOperationException($"Expected to find '{expected}'. Actual: {string.Join(" | ", values)}");
        }
    }

    private static void AssertEqual<T>(string label, T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
        }
    }
}
