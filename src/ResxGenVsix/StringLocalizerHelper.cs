using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections;
using System.Resources;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace ResxGenVsix
{
    /// <summary>
    /// Helper class for finding and generating resource keys for string localization.
    /// </summary>
    public class StringLocalizerHelper
    {
        /// <summary>
        /// Finds all resource keys in a given project.
        /// </summary>
        /// <param name="project">The project to analyze.</param>
        /// <returns>A dictionary of type names and their associated resource keys.</returns>
        public static Dictionary<string, List<string>> FindResourceKeys(Project project)
        {
            var allKeys = new Dictionary<string, List<string>>();
            var compilation = project.GetCompilationAsync().Result;
            foreach (var tree in compilation.SyntaxTrees)
            {
                var root = tree.GetRoot();
                var semanticModel = compilation.GetSemanticModel(tree);
                
                // Find name maps using different methods
                var nameMap1 = FindNameMapByPropertyAndField(root, semanticModel);
                var nameMap2 = FindNameMapByCreateMethod(compilation, tree, root, semanticModel);
                var nameMap3 = FindNameMapByPrimaryConstructor(root, semanticModel);
                // Merge name maps
                foreach (var name in nameMap2)
                {
                    if (!nameMap1.ContainsKey(name.Key))
                    {
                        nameMap1.Add(name.Key, name.Value);
                    }
                }
                foreach (var name in nameMap3)
                {
                    if (!nameMap1.ContainsKey(name.Key))
                    {
                        nameMap1.Add(name.Key, name.Value);
                    }
                }
                // Find resource keys and add them to allKeys
                var keys = FindResourceKey(tree, root, nameMap1, semanticModel);
                foreach (var key in keys)
                {
                    if (!allKeys.TryGetValue(key.Key, out List<string> value))
                    {
                        value = new List<string>();
                        allKeys.Add(key.Key, value);
                    }
                    value.AddRange(key.Value);
                }

                // Find resource keys from model validation attributes
                keys = FindResourceKeysByModelValidation(root, semanticModel);
                foreach (var key in keys)
                {
                    if (!allKeys.TryGetValue(key.Key, out List<string> value))
                    {
                        value = new List<string>();
                        allKeys.Add(key.Key, value);
                    }
                    value.AddRange(key.Value);
                }
            }

            return allKeys;
        }

        /// <summary>
        /// Generates resource files for the given resource keys.
        /// </summary>
        /// <param name="allTypeKeys">Dictionary of type names and their resource keys.</param>
        /// <param name="langs">Array of language codes.</param>
        /// <param name="rootNamespace">Root namespace of the project.</param>
        /// <param name="projectFullPath">Full path of the project.</param>
        /// <param name="resourcesPath">Path to the resources folder.</param>
        /// <param name="fileStyle">Whether to use file style for resource file names.</param>
        public static void GenResourceFiles(Dictionary<string, List<string>> allTypeKeys, string[] langs, string rootNamespace, string projectFullPath, string resourcesPath = "Resources", bool fileStyle = false)
        {
            int langCount = 1;
            if (langs != null && langs.Length > 0)
            {
                langCount = langs.Length + 1;
            }
            foreach (var typeMap in allTypeKeys)
            {
                var typeName = typeMap.Key;
                var resourceKeys = typeMap.Value;
                var fileName = ResourceFileFullName(rootNamespace, typeName, projectFullPath, resourcesPath, fileStyle);
                for (int i = 0; i < langCount; i++)
                {
                    var resourceFile = $"{fileName}.resx";
                    if (i != 0)
                    {
                        resourceFile = $"{fileName}.{langs[i - 1]}.resx";
                    }
                    Dictionary<string, string> allResourceKeys = ReadOrCreateResourceFile(resourceFile);
                    WriteResourceFile(resourceKeys, resourceFile, allResourceKeys);
                }
            }
        }

        /// <summary>
        /// Reads an existing resource file or creates a new one if it doesn't exist.
        /// </summary>
        /// <param name="resourceFile">Path to the resource file.</param>
        /// <returns>A dictionary of existing resource keys and values.</returns>
        private static Dictionary<string, string> ReadOrCreateResourceFile(string resourceFile)
        {
            var allKeys = new Dictionary<string, string>();
            if (!File.Exists(resourceFile))
            {
                using (ResXResourceWriter resx = new ResXResourceWriter(resourceFile))
                {
                    resx.Generate();
                }
                return allKeys;
            }
            using (ResXResourceReader resxReader = new ResXResourceReader(resourceFile))
            {
                resxReader.UseResXDataNodes = true;
                foreach (DictionaryEntry item in resxReader)
                {
                    if (!(item.Key is string key))
                    {
                        continue;
                    }
                    if (item.Value is ResXDataNode node)
                    {
                        var value = node.GetValue(typeResolver: null)?.ToString() ?? "";
                        allKeys.Add(key, value);
                    }
                }
            }
            return allKeys;
        }

        /// <summary>
        /// Writes resource keys to a resource file.
        /// </summary>
        /// <param name="resourceKeys">List of resource keys to write.</param>
        /// <param name="resourceFile">Path to the resource file.</param>
        /// <param name="allKeys">Dictionary of existing keys and values.</param>
        private static void WriteResourceFile(List<string> resourceKeys, string resourceFile, Dictionary<string, string> allKeys)
        {
            var notExistKeys = new Dictionary<string, string>(allKeys);
            using (ResXResourceWriter resx = new ResXResourceWriter(resourceFile))
            {
                foreach (var key in resourceKeys)
                {
                    if (notExistKeys.TryGetValue(key, out string value))
                    {
                        resx.AddResource(key, value);
                        notExistKeys.Remove(key);
                    }
                    else
                    {
                        resx.AddResource(key, key);
                    }
                }
                foreach (var key in notExistKeys)
                {
                    var node = new ResXDataNode(key.Key, key.Value)
                    {
                        Comment = "======DELETED======"
                    };
                    resx.AddResource(node);
                }
                resx.Generate();
            }
        }

        /// <summary>
        /// Finds name maps by analyzing Create method calls.
        /// </summary>
        /// <param name="compilation">The compilation.</param>
        /// <param name="tree">The syntax tree.</param>
        /// <param name="root">The root node of the syntax tree.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <returns>A dictionary of variable names and their associated type names.</returns>
        private static Dictionary<string, string> FindNameMapByCreateMethod(Compilation compilation, SyntaxTree tree, SyntaxNode root, SemanticModel semanticModel)
        {
            var nameMaps = new Dictionary<string, string>();
            var createCalls = root.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess
                        && memberAccess.Name.Identifier.Text == "Create");
            foreach (var call in createCalls)
            {
                if (call.Parent is AssignmentExpressionSyntax parent
                    && parent.Left is IdentifierNameSyntax left
                    && parent.Right is InvocationExpressionSyntax factory
                    && factory.Expression is MemberAccessExpressionSyntax memberAccess
                    && memberAccess.Expression is IdentifierNameSyntax member)
                {
                    var type = semanticModel.GetTypeInfo(member);
                    if (type.Type.Name == "IStringLocalizerFactory")
                    {
                        var variableName = left.Identifier.Text;
                        var firstArgument = call.ArgumentList.Arguments[0].Expression;
                        ProcessFirstArgument(firstArgument, variableName, semanticModel, nameMaps);

                    }
                }
            }
            return nameMaps;
        }

        /// <summary>
        /// Processes the first argument of a Create method call.
        /// </summary>
        /// <param name="firstArgument">The first argument expression.</param>
        /// <param name="variableName">The name of the variable being assigned.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="nameMaps">The dictionary to store the name mappings.</param>
        private static void ProcessFirstArgument(
           ExpressionSyntax firstArgument,
           string variableName,
           SemanticModel semanticModel,
           Dictionary<string, string> nameMaps)
        {
            if (firstArgument is LiteralExpressionSyntax literalExpression
                && literalExpression.IsKind(SyntaxKind.StringLiteralExpression))
            {
                if (!nameMaps.ContainsKey(variableName))
                {
                    nameMaps.Add(variableName, literalExpression.Token.ValueText);
                }
            }
            else
            {
                var classDeclaration = firstArgument.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (classDeclaration != null)
                {
                    if (semanticModel.GetDeclaredSymbol(classDeclaration) is INamedTypeSymbol classSymbol)
                    {
                        var typeName = classSymbol.ToDisplayString();
                        if (!nameMaps.ContainsKey(typeName))
                        {
                            nameMaps.Add(variableName, typeName);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Finds name maps by analyzing properties and fields.
        /// </summary>
        /// <param name="root">The root node of the syntax tree.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <returns>A dictionary of variable names and their associated type names.</returns>
        private static Dictionary<string, string> FindNameMapByPropertyAndField(SyntaxNode root, SemanticModel semanticModel)
        {
            var nameMaps = new Dictionary<string, string>();

            var properties = root.DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .Where(p => p.Type is GenericNameSyntax genericName
                    && (genericName.Identifier.Text == "IStringLocalizer" || genericName.Identifier.Text == "IHtmlLocalizer"));

            foreach (var property in properties)
            {
                var variableName = property.Identifier.Text;
                AddToNameMaps(variableName, property.Type, semanticModel, nameMaps);
            }

            var fields = root.DescendantNodes()
                .OfType<VariableDeclarationSyntax>()
                .Where(f => f.Type is GenericNameSyntax genericName
                    && (genericName.Identifier.Text == "IStringLocalizer" || genericName.Identifier.Text == "IHtmlLocalizer"));

            foreach (var field in fields)
            {
                foreach (var variable in field.Variables)
                {
                    var variableName = variable.Identifier.Text;
                    AddToNameMaps(variableName, field.Type, semanticModel, nameMaps);
                }
            }

            return nameMaps;
        }

        /// <summary>
        /// Adds a name mapping to the dictionary.
        /// </summary>
        /// <param name="variableName">The name of the variable.</param>
        /// <param name="typeSyntax">The type syntax of the variable.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="nameMaps">The dictionary to store the name mappings.</param>
        private static void AddToNameMaps(string variableName, TypeSyntax typeSyntax, SemanticModel semanticModel, Dictionary<string, string> nameMaps)
        {
            var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
            if (typeInfo.Type is INamedTypeSymbol namedTypeSymbol)
            {
                var typeArguments = namedTypeSymbol.TypeArguments;
                if (typeArguments.Any())
                {
                    var typeName = typeArguments.First().ToDisplayString();
                    if (!nameMaps.ContainsKey(variableName))
                    {
                        nameMaps.Add(variableName, typeName);
                    }
                }
            }
        }

        /// <summary>
        /// Finds resource keys from model validation attributes.
        /// </summary>
        /// <param name="root">The root node of the syntax tree.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <returns>A dictionary of class names and their associated resource keys.</returns>
        private static Dictionary<string, List<string>> FindResourceKeysByModelValidation(SyntaxNode root, SemanticModel semanticModel)
        {
            var nameMaps = new Dictionary<string, List<string>>();
            var propertyDeclarations = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();

            foreach (var property in propertyDeclarations)
            {
                var classDeclaration = property.FirstAncestorOrSelf<ClassDeclarationSyntax>();

                if (classDeclaration == null)
                {
                    continue;
                }
                var classFullName = GetFullClassName(classDeclaration);

                var attributes = property.AttributeLists.SelectMany(attrList => attrList.Attributes);

                foreach (var attribute in attributes)
                {
                    var attributeName = attribute.Name is IdentifierNameSyntax identifierNameSyntax ? identifierNameSyntax.Identifier.Text : "";
                    var attributeTypeInfo = semanticModel.GetTypeInfo(attribute);
                    if (attributeTypeInfo.Type != null
                        && attributeTypeInfo.Type.ToDisplayString().StartsWith("System.ComponentModel.DataAnnotations."))
                    {
                        var argument = attribute.ArgumentList?.Arguments
                            .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.Text == "ErrorMessage" ||
                            (arg.NameEquals?.Name.Identifier.Text == "Name"));
                        
                        if (argument != null)
                        {
                            var resourceKey = semanticModel.GetConstantValue(argument.Expression).Value;
                            if(!nameMaps.TryGetValue(classFullName, out var resources))
                            {
                                nameMaps[classFullName]=new List<string>();
                            }
                            nameMaps[classFullName].Add(resourceKey.ToString());
                        }
                    }

                }
            }
            return nameMaps;
        }

        /// <summary>
        /// Gets the full class name including namespace.
        /// </summary>
        /// <param name="classDeclaration">The class declaration syntax.</param>
        /// <returns>The full class name.</returns>
        private static string GetFullClassName(ClassDeclarationSyntax classDeclaration)
        {
            var className = classDeclaration.Identifier.Text;

            var namespaceDeclaration = classDeclaration.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();

            if (namespaceDeclaration != null)
            {
                var namespaceName = namespaceDeclaration.Name.ToString();
                return $"{namespaceName}.{className}";
            }
            else
            {
                return className;
            }
        }

        /// <summary>
        /// Finds resource keys by analyzing IStringLocalizer and IHtmlLocalizer usages.
        /// </summary>
        /// <param name="tree">The syntax tree.</param>
        /// <param name="root">The root node of the syntax tree.</param>
        /// <param name="nameMaps">Dictionary of variable names and their associated type names.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <returns>A dictionary of type names and their associated resource keys.</returns>
        private static Dictionary<string, List<string>> FindResourceKey(
            SyntaxTree tree, SyntaxNode root, Dictionary<string, string> nameMaps, SemanticModel semanticModel)
        {
            var resourceKeys = new Dictionary<string, List<string>>();

            var localizers = root.DescendantNodes()
                .OfType<ElementAccessExpressionSyntax>()
                .Where(e => e.Expression is IdentifierNameSyntax idName && nameMaps.ContainsKey(idName.Identifier.Text));
            if (!localizers.Any())
            {
                return resourceKeys;
            }
            foreach (var localizer in localizers)
            {
                if (!(localizer.Expression is IdentifierNameSyntax identifierName))
                {
                    continue;
                }
                if (!nameMaps.TryGetValue(identifierName.Identifier.Text, out var typeName))
                {
                    continue;
                }
                if (string.IsNullOrEmpty(typeName))
                {
                    var type = semanticModel.GetTypeInfo(identifierName);
                    if ((type.Type?.Name == "IStringLocalizer" || type.Type?.Name == "IHtmlLocalizer") && type.Type is INamedTypeSymbol namedTypeSymbol)
                    {
                        var firstTypeArgument = namedTypeSymbol.TypeArguments.FirstOrDefault();
                        if (firstTypeArgument != null)
                        {
                            typeName = firstTypeArgument.Name;
                            if (!nameMaps.ContainsKey(identifierName.Identifier.Text))
                            {
                                nameMaps.Add(identifierName.Identifier.Text, typeName);
                            }
                        }
                    }
                }
                if (!string.IsNullOrEmpty(typeName))
                {
                    if (!resourceKeys.TryGetValue(typeName, out var key))
                    {
                        key = new List<string>();
                        resourceKeys.Add(typeName, key);
                    }
                    var stringArgument = localizer.ArgumentList.Arguments[0].ToString().Trim('"');
                    key.Add(stringArgument);
                }
            }
            return resourceKeys;
        }

        /// <summary>
        /// Generates the full path for a resource file.
        /// </summary>
        /// <param name="rootNamespace">The root namespace of the project.</param>
        /// <param name="typeFullName">The full name of the type.</param>
        /// <param name="projectFullPath">The full path of the project.</param>
        /// <param name="resouresPath">The path to the resources folder.</param>
        /// <param name="fileStyle">Whether to use file style for resource file names.</param>
        /// <returns>The full path of the resource file.</returns>
        private static string ResourceFileFullName(string rootNamespace, string typeFullName, string projectFullPath, string resouresPath = "Resources", bool fileStyle = false)
        {
            var fileName = typeFullName;
            var resourcesFullPath = Path.Combine(projectFullPath, resouresPath);
            if (!Directory.Exists(resourcesFullPath))
            {
                Directory.CreateDirectory(resourcesFullPath);
            }
            if (typeFullName.StartsWith($"{rootNamespace}.", StringComparison.OrdinalIgnoreCase))
            {
                fileName = typeFullName.Substring(rootNamespace.Length + 1);
            }
            var dirs = fileName.Split('.');
            if (fileStyle || dirs.Length == 1)
            {
                return Path.Combine(resourcesFullPath, fileName);
            }
            var resourcePath = resourcesFullPath;
            for (var i = 0; i < dirs.Length - 1; i++)
            {
                resourcePath = Path.Combine(resourcePath, dirs[i]);
                if (!Directory.Exists(resourcePath))
                {
                    Directory.CreateDirectory(resourcePath);
                }
            }
            return Path.Combine(resourcePath, dirs[dirs.Length - 1]);
        }

        private static Dictionary<string, string> FindNameMapByPrimaryConstructor(SyntaxNode root, SemanticModel semanticModel)
        {
            var nameMaps = new Dictionary<string, string>();
            
            // 查找所有类声明
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            
            foreach (var cls in classes)
            {
                // 只检查主构造函数参数
                if (cls.ParameterList != null)
                {
                    var localizerParameter = cls.ParameterList.Parameters
                        .FirstOrDefault(p => IsStringLocalizerType(p.Type));
                        
                    if (localizerParameter != null)
                    {
                        // 获取类的完整名称
                        var classSymbol = semanticModel.GetDeclaredSymbol(cls) as INamedTypeSymbol;
                        var className = classSymbol?.ToDisplayString();
                        
                        if (!string.IsNullOrEmpty(className))
                        {
                            // 添加到映射字典中
                            nameMaps[localizerParameter.Identifier.Text] = className;
                        }
                    }
                }
            }
            
            return nameMaps;
        }

        private static bool IsStringLocalizerType(TypeSyntax type)
        {
            var typeString = type.ToString();
            return typeString == "IStringLocalizer" 
                || typeString == "IHtmlLocalizer"
                || typeString.StartsWith("IStringLocalizer<")
                || typeString.StartsWith("IHtmlLocalizer<");
        }
    }
}