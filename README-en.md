# Automatic Localization Resource File Generator

This is a VSIX extension designed to automate the generation of localization resource files, simplifying the localization process in your projects. With a simple configuration and a right-click operation, you can quickly generate `.resx` resource files for multiple languages.

## Features

Right-click on the project where you want to generate localization resource files and select **Generate Localization Resource Files**. The tool will then generate the required `.resx` files.

## Usage

1. **Install the Extension**: Download and install this VSIX extension.
2. **Configuration File (Optional)**: Create a `ResxGen.json` file in the root directory of your project to configure the behavior of localization file generation.
3. **Right-click to Generate**: Right-click on the project and select **Generate Localization Resource Files** to generate the necessary `.resx` files.

## ResxGen.json Configuration

The `ResxGen.json` file allows you to customize the languages, structure, and path of the generated resource files. The configuration format is as follows:

```json
{
    "Langs": ["zh-CN", "en-US"], // Languages for which localization files will be generated. By default, only a neutral resource file is generated.
    "IsFileStyle": false,        // The directory structure of resource files, whether to generate them in a file-based manner.
    "ResourcesPath": "Resources" // The path where resource files will be generated.
}
```

### Configuration Details

- **Langs**: A list of languages for which resource files will be generated. By default, only a neutral resource file (without language-specific identifiers) is generated.
- **IsFileStyle**: Controls the directory structure for the generated resource files. If set to `true`, resource files will follow a file-based structure. For example, if the `HomeController` class is in the `Controllers` directory, the generated resource file will be named `Controllers.HomeController.resx`. If set to `false`, resource files will follow a directory-based structure, with files named based on the class only. For instance, `HomeController.resx` will be placed in the `Resources/Controllers` directory.
- **ResourcesPath**: Specifies the path where the resource files will be generated. By default, files will be generated in the `Resources` directory of the project.

### Example

By default, if no `ResxGen.json` file is created, the tool will only generate a neutral resource file and place it in the `Resources` directory.

If you create the following configuration file:

```json
{
    "Langs": ["zh-CN", "en-US"],
    "IsFileStyle": true,
    "ResourcesPath": "Localization"
}
```

The tool will generate resource files for Chinese and English. If `IsFileStyle` is `true`, the resource files will be `Controllers.HomeController.resx`. If `IsFileStyle` is `false`, the resource files will be named `HomeController.resx` and placed in the `Localization/Controllers` directory.

---

### Scenarios for Automatic Generation of Localization Resource Files

Localization resource files (`.resx`) will be automatically generated in the following cases:

1. **Keys used with `_localizer`**:
   All the keys referenced in `IStringLocalizer` instances will be collected and added to the `.resx` file. For example:

   ```csharp
   public class HomeController : Controller
   {
       // Localizer for HomeController's resources
       private readonly IStringLocalizer<HomeController> _localizer;
       public HomeController(IStringLocalizer<HomeController> localizer)
       {
           _localizer = localizer;
       }
   }
   ```
   In this scenario, any keys referenced by `_localizer` within the `HomeController` class will be collected.

2. **Localizers created using a factory**:
   When `IStringLocalizer` is created using `IStringLocalizerFactory`, the tool can collect keys from these instances as well. Example:

   ```csharp
   public class HomeController : Controller
   {
       private readonly IStringLocalizer _localizer;
       private readonly IStringLocalizer _localizer2;

       public HomeController(IStringLocalizerFactory localizerFactory)
       {
           _localizer = localizerFactory.Create(typeof(HomeController));
           _localizer2 = localizerFactory.Create("Controllers.HomeController", Assembly.GetExecutingAssembly().FullName);
       }
   }
   ```
   In this case, keys for both `_localizer` and `_localizer2` will be generated and added to the resource file.

3. **Model validation messages and display attributes**:
   Validation attributes such as `ErrorMessage` in `DataAnnotations`, as well as `Display` attribute names, will also be automatically localized. Example:

   ```csharp
   public class RegisterDto
   {
       [Display(Name = "User name")]
       [Required(ErrorMessage = "{0} is required.")]
       public string UserName { get; set; }

       [Required(ErrorMessage = "{0} is required.")]
       [StringLength(8, ErrorMessage = "PasswordLeastCharactersLong", MinimumLength = 6)]
       public string Password { get; set; }

       [Compare("Password", ErrorMessage = "PasswordDoNotMatch")]
       public string ConfirmPassword { get; set; }
   }
   ```

   The tool will extract the `Display(Name)` values and any error messages defined in validation attributes, and generate them into the resource file.

By scanning the above elements in your code, the tool ensures that all relevant localization keys are collected and placed into the appropriate `.resx` files, following the configuration you define in the `ResxGen.json` file.

---

For any issues or suggestions, feel free to submit an issue!

---