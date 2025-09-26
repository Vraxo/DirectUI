using SharpGen.Runtime;
using Vortice.D3DCompiler;
using Vortice.Direct3D;

namespace DirectUI.Backends;

public static class ShaderCompiler
{
    public static Blob Compile(string source, string entryPoint, string profile)
    {
        // Calling the overload with positional arguments to avoid naming errors.
        // This is the 10-argument overload.
        // The error suggests the entryPoint and sourceFileName parameters are swapped in the user's library version.
        Result compileResult = Compiler.Compile(
            source,          // 1: string sourceCode
            null,            // 2: ShaderMacro[] defines
            null,            // 3: Include include
            entryPoint,      // 4: string entryPoint (was "ShaderSource")
            "ShaderSource",  // 5: string sourceFileName (was entryPoint)
            profile,         // 6: string profile
            ShaderFlags.None,// 7: ShaderFlags shaderFlags
            EffectFlags.None,// 8: EffectFlags effectFlags
            out Blob? code,  // 9: out Blob result
            out Blob? error  // 10: out Blob errorBlob
        );

        // Check if the compilation failed.
        if (compileResult.Failure)
        {
            string errorMessage = "Shader compilation failed";
            if (error is not null)
            {
                // The error blob contains detailed diagnostics from the compiler.
                errorMessage = error.AsString();
                error.Dispose();
            }
            code?.Dispose(); // Clean up the code blob if it was created.

            // Log the detailed error to the console before throwing.
            Console.WriteLine($"--- SHADER COMPILATION FAILED ---");
            Console.WriteLine($"Entry Point: {entryPoint}");
            Console.WriteLine($"Profile: {profile}");
            Console.WriteLine($"Error: {errorMessage}");
            Console.WriteLine("--- Shader Source ---");
            Console.WriteLine(source);
            Console.WriteLine("---------------------");

            // Throw an exception with the detailed message from the compiler.
            throw new SharpGenException(compileResult, errorMessage);
        }

        // On success, the error blob is usually null, but dispose it just in case.
        error?.Dispose();

        if (code is null)
        {
            // This should not happen if compilation succeeded, but it's a good safeguard.
            throw new InvalidOperationException("Shader compilation succeeded but the resulting blob is null.");
        }

        return code;
    }
}