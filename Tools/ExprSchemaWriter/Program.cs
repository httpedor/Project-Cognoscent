using Rpg.Scripting;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: ExprSchemaWriter <output-path>");
    Console.Error.WriteLine("  Writes the auto-generated expression JSON schema to the specified file.");
    return 1;
}

var outputPath = args[0];
ExprJsonSchema.WriteToFile(outputPath);
Console.WriteLine($"Expression schema written to: {outputPath}");
return 0;
