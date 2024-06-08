using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using static FriedLang.NativeLibraries.Lang;

namespace FlangWebsiteConsole
{
    public class FlangHTMLParser : AnalizerBase<char>
    {
        public FlangHTMLParser() : base('\0') { }
        public struct Macro 
        {
            public Macro(string name, string body, params string[] arguments) 
            {
                this.name = name;
                this.body = body;
                this.arguments = arguments.ToList();
            }
            public string name;
            public List<string> arguments;
            public string body;
        }
        public struct ClientFunction
        {
            public string name;
            public List<(string, string)> arguments;
        }
        //#define get($id) document.getElementById($id)
        //#define setText($element, $text) $element.innerText = $text
        List<Macro> DefaultMacros = new List<Macro>()
        {
            new Macro("get","document.getElementById({0})","$id"),
            new Macro("set","innerHtml = {0}","$text"),
            new Macro("setText","{0}.innerText = {1}","$element", "$text"),

            new Macro("bind","id=\"BIND_{0}\"","$varible"),
            new Macro("setBind",
                "document.addEventListener('DOMContentLoaded', function() {{ const BIND_REF_{0} = document.getElementById('BIND_{0}'); function BIND_update{0}() {{ {0} = BIND_REF_{0}.value;}} BIND_update{0}(); BIND_REF_{0}.addEventListener('input', BIND_update{0}); }});",
                "$varible"),
        };

        private string ClientCodePreParser(string input)
        {
            string FinalText = string.Empty;

            bool isClientFlang = false;
            string FinalClientCode = string.Empty;
            List<ClientFunction> clientFunctions = new List<ClientFunction>();

            while (Current != '\0')
            {
                if (isClientFlang)
                {
                    if (Find(")>"))
                    {
                        isClientFlang = false;
                        continue;
                    }
                    else if (Find(" clientFlang "))
                    {
                        FinalClientCode += " ";
                        var functionName = "";
                        while (Current != '(')
                        {
                            functionName += Current;
                            FinalClientCode += Current;
                            Position++;
                        }

                        FinalClientCode += Current;
                        Position++; //skip (

                        ClientFunction function = new ClientFunction();
                        function.name = functionName;
                        function.arguments = new List<(string, string)>();

                        while (Current != ')')
                        {
                            string type = "";
                            string argument = "";
                            bool isArgName = false;
                            while (Current != ',' && Current != ')')
                            {
                                if (isArgName)
                                {
                                    argument += Current;
                                    FinalClientCode += Current;
                                    Position++;
                                }
                                else
                                {
                                    if (Current == ' ')
                                    {
                                        isArgName = true;
                                        FinalClientCode += Current;
                                        Position++;
                                        continue;
                                    }
                                    type += Current;
                                    FinalClientCode += Current;
                                    Position++;
                                }
                            }
                            function.arguments.Add((type, argument));
                            if (Current == ')') break;
                            Position++; //skip ,
                        }
                        FinalClientCode += Current;
                        Position++; //skip )

                        clientFunctions.Add(function);
                    }
                    else
                    {
                        FinalClientCode += Current;
                        //FinalText += Current;
                        Position++;
                    }
                }
                else
                {
                    if (Find("<(clientflang"))
                    {
                        isClientFlang = true;
                        Position++;
                        continue;
                    }
                    if (Find("<(@"))
                    {
                        string functionName = "";
                        while (Current != ')' && Peek(1) != '>')
                        {
                            functionName += Current;
                            Position++;
                        }
                        Position++;
                        Position++;
                        FinalText += "GENERATED_JS_"+functionName;

                        //if (!clientFunctions.Contains(functionName))
                        //    clientFunctions.Add(functionName);
                        continue;
                    }
                    else
                    {
                        FinalText += Current;
                        Position++;
                    }
                }
            }




            string clientEventHandeler = $$"""
            <script>
""";
            foreach (var func in clientFunctions)
            {
                List<string> argumentz = func.arguments.Select((ar) => ar.Item2).ToList();
                clientEventHandeler += $$"""
            	async function GENERATED_JS_{{func.name}}({{string.Join(", ",argumentz)}})
            	{
            		<(flang
            			if (URL.contains("?"))
            				print("var url = \"{URL}&clientEvent={{func.name}}\";"$);
            			else
            				print("var url = \"{URL}?clientEvent={{func.name}}\";"$);
            		)>


                    // Define your POST parameters here
                    let postData = {
""";
                    foreach (var (type, name) in func.arguments)
                    {
                    clientEventHandeler += $$"""
                        GENERATED_{{name}}: {{name}},
""";

                    }
clientEventHandeler += $$"""
                    };
                    
                    // Send POST request
                    let response = await fetch(url, {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify(postData)
                    });
                    
                    // Get the response body
                    let responseData = await response.text();
                    return responseData;
            	}
""";
            }
clientEventHandeler += $$"""
            </script>
""";

            string output = $$"""
            <(flang
                {{FinalClientCode}}

""";
            foreach (var func in clientFunctions)
            {
                output += $$"""
                    if (GET["clientEvent"] == "{{func.name}}")
                    {
""";
                    foreach (var (type, name) in func.arguments)
                    {
output += $$"""
                        var% {{name}} = POST["GENERATED_{{name}}"];
""";

                    }
                List<string> argumentz = func.arguments.Select((ar) => ar.Item2).ToList();

                output += $$"""
                        var output = {{func.name}}({{string.Join(", ", argumentz)}});
                        return "$clientEvent${output}"$;
                    }
""";
            }
output += $$"""
            )>
            {{FinalText.Replace("<$clientEventHandeler$>", clientEventHandeler)}}

""";

            return output;
        }

        public string ParseMacros(string input) 
        {
            string FinalText = string.Empty;

            List<Macro> Macros = new List<Macro>();
            Macros.AddRange(DefaultMacros);

            while (Current != '\0')
            {
                if (Find("#define "))
                {
                    string macroName = "";
                    List<string> arguments = new List<string>();
                    string macroBody = "";

                    while (Current != '(')
                    {
                        macroName += Current;
                        Position++;
                    }
                    Position++;
                    while (Current != ')')
                    {
                        string argument = "";
                        while (Current != ',' && Current != ')')
                        {
                            if (Current == ' ')
                            {
                                Position++;
                                continue;
                            }
                            argument += Current;
                            Position++;
                        }
                        arguments.Add(argument);
                        if (Current == ')') break;
                        Position++; //skip ,
                    }
                    Position++; // skip )
                    while (Current == ' ') //skip all spaces
                    {
                        Position++;
                    }
                    while (Current != '\n' && Current != '\r') //the body
                    {
                        bool found = false;
                        for (int i = 0; i < arguments.Count(); i++)
                        {
                            var argument = arguments[i];
                            if (Find(argument))
                            {
                                macroBody += "{" + i + "}";
                                // generate a string format "console.writeline({0})".format(arg1,arg2)
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            macroBody += Current;
                            Position++;
                        }
                    }

                    var finalMacro = new Macro()
                    {
                        name = macroName,
                        arguments = arguments,
                        body = macroBody
                    };

                    if (!Macros.Contains(finalMacro))
                        Macros.Add(finalMacro);
                    continue;
                }
                else
                {
                    bool found = false;
                    // Expand macros
                    for (int i = 0; i < Macros.Count(); i++)
                    {
                        var macro = Macros[i];
                        if (Find($"@{macro.name}("))
                        {
                            List<string> arguments = new List<string>();
                            while (Current != ')')
                            {
                                string argument = "";
                                while (Current != ',' && Current != ')')
                                {
                                    argument += Current;
                                    Position++;
                                }
                                arguments.Add(argument);
                                if (Current == ')') break;
                                Position++; //skip ,
                            }
                            Position++; //skip )

                            int macroArgCount = macro.arguments.Count();
                            int argCount = arguments.Count();

                            if (argCount == macroArgCount)
                            {
                                var args = new object[argCount];
                                for (int j = 0; j < argCount; j++)
                                {
                                    args[j] = arguments[j];
                                }
                                FinalText += string.Format(macro.body, args);
                            }
                            else
                            {
                                throw new Exception($"error on macro {macro.name}, expected {macroArgCount} arguments, got {argCount}");
                            }

                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        FinalText += Current;
                        Position++;
                    }
                }
            }

            return FinalText;
        }
        public string ParseMacrosOld(string input) 
        {
            var macroLines = input.Split("\n").Where(l => l.Trim().StartsWith("#define")).ToList();
            foreach (var macroLine in macroLines)
            {
                var macroText = macroLine.Trim().Substring("#define ".Length);
                string macroName = "";
                List<string> macroArguments = new List<string>();
                string currentArgument = "";
                string macroBody = "";
                bool isName = true;
                bool isArguments = true;

                foreach (char character in macroText)
                {
                    if (isName)
                    {
                        if (char.IsLetter(character))
                        {
                            macroName += character;
                        }
                        else
                        {
                            if (character == '(')
                            {
                                isName = false;
                                isArguments = true;
                            }
                        }
                    }
                    else if (isArguments)
                    {
                        if (char.IsLetter(character))
                        {
                            macroName += character;
                        }
                        else
                        {
                            if (character == '(')
                            {
                                isName = false;
                                isArguments = true;
                            }
                        }
                    }
                }
            }

            return "";
        }
        public (string FinalText, string FinalCode) Parse(string input)
        {
            string FinalText = string.Empty;
            string FinalCode = string.Empty;
            this.Analizable = input.ToList();
            bool isFlang = false;
            bool isFPrint = false;


            bool useMacros = input.Split("\n").Any(l => l.Trim().StartsWith("#use macros"));
            if (useMacros)
            {
                input = input.Replace("#use macros", "");
                this.Analizable = input.ToList();
                input = ParseMacros(input);
                this.Analizable = input.ToList();
                this.Position = 0;

            }
            else
            {
                if (input.Contains("#define"))
                {
                    throw new System.Exception("\"#define\" macro definition has been found, but macros arent enabled, add \"#use macros\" to the top of your file to enable.");
                }
            }
            bool useClient = input.Split("\n").Any(l => l.Trim().StartsWith("#use clientFlang"));
            if (useClient)
            {
                if (!input.Contains("<$clientEventHandeler$>"))
                {
                    throw new System.Exception("clientEventHandeler is missing, client code cannot be executed, \"<$clientEventHandeler$>\" in the head of your html file");
                }
                if (!input.Contains("<(clientflang"))
                {
                    throw new System.Exception("#use clientFlang is specified but no \"<(clientflang\" code entry can be found, which means the implementations are missing");
                }
                input = input.Replace("#use clientFlang", "");
                this.Analizable = input.ToList();
                var output = ClientCodePreParser(input);
                this.Analizable = output.ToList();
                this.Position = 0;
            }
            else
            {
                if (input.Contains("<(clientflang"))
                {
                    throw new System.Exception("\"<(clientflang\" code entry has been found, but clientFlang isnt enabled, add \"#use clientFlang\" to the top of your file to enable.");
                }
            }

            while (Current != '\0')
            {
                if (isFlang)
                {
                    if (Find(")>"))
                    {
                        if (isFPrint)
                        { 
                            isFPrint = false;
                            FinalCode += ")";
                        }
                        isFlang = false;
                        continue;
                    }
                    else
                    {
                        FinalCode += Current;
                        Position++;
                    }
                }
                else
                {
                    if (Find("<(flang"))
                    {
                        isFlang = true;
                        //FinalText += Current;
                        Position++;
                        FinalCode += $" POSITION = {FinalText.Length}; ";
                        continue;
                    }
                    if (Find("<(="))
                    {
                        isFlang = true;
                        isFPrint = true;
                        //FinalText += Current;
                        //Position++;
                        FinalCode += $" POSITION = {FinalText.Length}; print(";
                        continue;
                    }
                    else
                    {
                        FinalText += Current;
                        Position++;
                    }
                }
            }

            return (FinalText, FinalCode);
        }
        public bool Find(string find)
        {
            for (int i = 0; i < find.Length; i++)
            {
                if (Peek(i) == find[i])
                {
                    continue;
                }
                else return false;
            }
            Position += find.Length;
            return true;
        }
    }
}
