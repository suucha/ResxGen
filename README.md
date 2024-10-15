# 本地化资源文件自动生成器

这是一个VSIX扩展，用于自动生成本地化资源文件，简化项目中的本地化任务。通过简单的配置和右键操作，您可以快速生成多个语言的`.resx`资源文件。

## 功能

在需要生成本地化资源文件的项目上点击右键，选择 **生成本地化资源文件**，工具将生成相应的`.resx`文件。

## 使用方法

1. **安装扩展**：下载并安装此VSIX扩展。
2. **配置文件（可选）**：在项目的根目录下创建 `ResxGen.json` 文件，以配置本地化文件生成的行为。
3. **右键生成**：右键点击项目，选择 **生成本地化资源文件**，生成所需的 `.resx` 文件。

## ResxGen.json 配置

`ResxGen.json` 文件允许您自定义生成的资源文件的语言、结构和路径。配置格式如下：

```json
{
    "Langs": ["zh-CN", "en-US"], // 生成本地化资源文件的语言。默认只生成中性资源文件。
    "IsFileStyle": false,        // 资源文件的目录结构，是否采用文件方式生成。
    "ResourcesPath": "Resources" // 资源文件生成的路径。
}
```

### 配置说明

- **Langs**：生成本地化资源文件的语言列表。默认情况下，仅生成中性资源文件（不带语言标识符的资源文件）。
- **IsFileStyle**：控制生成的资源文件的目录结构。如果设置为 `true`，资源文件将按照文件结构生成。例如，如果 `HomeController` 类在 `Controllers` 目录中，生成的资源文件将命名为 `Controllers.HomeController.resx`。如果设置为 `false`，资源文件将遵循目录结构，仅使用类名作为文件名。例如，`HomeController.resx` 将放置在 `Resources/Controllers` 目录下。
- **ResourcesPath**：指定生成的资源文件的路径。默认情况下，资源文件将生成在项目的 `Resources` 目录中。

### 示例

默认情况下，如果没有创建 `ResxGen.json` 文件，工具只会生成一个中性资源文件，并将其放置在 `Resources` 目录中。

如果您创建了以下配置文件：

```json
{
    "Langs": ["zh-CN", "en-US"],
    "IsFileStyle": true,
    "ResourcesPath": "Localization"
}
```

工具将为中文和英文生成资源文件。如果 `IsFileStyle` 为 `true`，则资源文件将为 `Controllers.HomeController.resx`。如果 `IsFileStyle` 为 `false`，则资源文件将命名为 `HomeController.resx`，并放置在 `Localization/Controllers` 目录下。

---

### 自动生成本地化资源文件的场景

在以下情况下，本地化资源文件（`.resx`）将自动生成：

1. **使用 `_localizer` 的 key**：
   所有在 `IStringLocalizer` 实例中引用的 key 都会被收集并添加到 `.resx` 文件中。例如：

   ```csharp
   public class HomeController : Controller
   {
       // 提供 HomeController 区域性资源的 Localizer
       private readonly IStringLocalizer<HomeController> _localizer;
       public HomeController(IStringLocalizer<HomeController> localizer)
       {
           _localizer = localizer;
       }
   }
   ```
   在这种情况下，`HomeController` 类中引用的所有 `_localizer` 的 key 都将被收集。

2. **使用工厂创建的 Localizer**：
   当使用 `IStringLocalizer

Factory` 创建 `IStringLocalizer` 时，工具也可以收集这些实例中的 key。例如：

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
   在这种情况下，`_localizer` 和 `_localizer2` 的 key 都将生成并添加到资源文件中。

3. **模型验证信息和显示属性**：
   数据注解中的验证属性（如 `ErrorMessage`）和 `Display` 属性的 `Name` 也将自动本地化。例如：

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

   工具将提取 `Display(Name)` 的值以及验证属性中定义的错误消息，并将其生成到资源文件中。

通过扫描代码中的上述元素，工具确保所有相关的本地化 key 都被收集并放置到适当的 `.resx` 文件中，按照您在 `ResxGen.json` 文件中定义的配置进行生成。

---

如有问题或建议，请提交问题反馈！

--- 