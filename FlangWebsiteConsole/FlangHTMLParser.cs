﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using static FlangWebsiteConsole.FlangHTMLParser;
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
            public bool DoubleDefined;
            public bool Component;
            public bool ComponentHasBody;
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
            new Macro("set","innerText = {0}","$text"),
            new Macro("setText","{0}.innerText = {1}","$element", "$text"),

            new Macro("bind","id=\"BIND_{0}\"","$varible"),
            new Macro("setBind",
                "document.addEventListener('DOMContentLoaded', function() {{ const BIND_REF_{0} = document.getElementById('BIND_{0}'); function BIND_update{0}() {{ {0} = BIND_REF_{0}.value;}} BIND_update{0}(); BIND_REF_{0}.addEventListener('input', BIND_update{0}); }});",
                "$varible"),

            new Macro("htmlcode",")>{0}<(flang","$htmlcode"){ DoubleDefined=true },
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
                    if (Find("<(clientFlang"))
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


                    // Data to be sent in the POST request
                    const postData = new URLSearchParams();
""";
                    foreach (var (type, name) in func.arguments)
                    {
                    clientEventHandeler += $$"""
                        postData.append('GENERATED_{{name}}', {{name}});
""";

                    }
clientEventHandeler += $$"""
                    
                    // Making the fetch request
                    let response = await fetch(url, {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/x-www-form-urlencoded'
                        },
                        body: postData.toString()
                    })
                    
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
                        
                        string STR_{{name}} = POST["GENERATED_{{name}}"];
                        {{type}} {{name}} = STR_{{name}};
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

        public bool ParseMacroNormal(ref string macroName)
        {
            bool doubleDefine = false;
            while (Current != '(')
            {
                macroName += Current;
                Position++;
            }
            Position++; //to skip (
            if (Current == '(')
            {
                Position++; // to skip the second ( and enable double defined
                doubleDefine = true;
            }

            return doubleDefine;
        }
        public bool ParseMacroComponent(ref string macroName)
        {
            bool componentHasBody = false;
            Position++;
            while (Current != '/' && Current != '>')
            {
                macroName += Current;
                Position++;
            }
            if (Current != '/') //its a has a body
            {
                componentHasBody = true;
            }
            else
            { 
                Position++; //skip /
            }
            Position++; // skip >
            Position++; // skip (


            return componentHasBody;
        }
        public void ParseMacroArguments(ref List<string> arguments, bool doubleDefine)
        {
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
                if (doubleDefine && Current == ',') throw new Exception("DoubleDefined macros does not support multiple arguments");
                arguments.Add(argument);
                if (Current == ')') break;
                Position++; //skip ,
            }
            Position++; // skip )
            if (doubleDefine && Current == ')') Position++;
        }
        public void ParseMacroBody(List<string> arguments, ref string macroBody) 
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
        public void CaptureNormalMacroArguments(bool doubleDefined, ref List<string> arguments)
        {
            if (doubleDefined && Current == '(')
            {
                Position++; //skip the extra (
                            //in this case we're using the double defined version
                string argument = "";
                while (!(Current == ')' && Peek(1) == ')'))
                {
                    argument += Current;
                    Position++;
                }
                arguments.Add(argument);
                Position++; //skip first )
                Position++; //skip second )
            }
            else
            {
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
            }
        }
        public void CaptureComponentMacroTypedArguments(Macro macro, ref List<string> arguments)
        { 
            while (char.IsWhiteSpace(Current)) Position++;
            foreach (string argument in macro.arguments) 
            {
                string argumentValue = "";
                bool found = false;
                if (Find($"{argument}=\""))
                {
                    found = true;
                    while (Current != '"')
                    {
                        argumentValue += Current;
                        Position++;
                    }
                    Position++; //skip the last "
                }
                if (found)
                { 
                    //the index where this argument is stored
                    int idx = macro.arguments.IndexOf(argument);
                    //effectively setting that value
                    arguments[idx] = argumentValue;
                    break;
                }
            }
        }
        public void CaptureComponentMacroArguments(Macro macro, ref List<string> arguments)
        {
            if (macro.ComponentHasBody)
            {
                while (Current != '>')
                { 
                    //normal arguments
                    CaptureComponentMacroTypedArguments(macro, ref arguments);
                }
                // capture the body argument
                Position++; //skip >
                string BodyArgument = "";
                while (!Find($"</{macro.name}>")) //till we find the closing tag
                {
                    BodyArgument += Current;
                    Position++;
                }
                //body argument is always stored at index 0
                arguments[0] = BodyArgument;
                //we're done here
            }
            else
            {
                while (!(Current == '/' && Peek(1) == '>'))
                { 
                    //normal arguments
                    CaptureComponentMacroTypedArguments(macro, ref arguments);
                }
                Position++; //skip '/'
                Position++; //skip '>'
            }
        }
        public void ApplyMacro(Macro macro, List<string> arguments, ref string FinalText, ref bool found) 
        {
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
                    bool doubleDefine = false;
                    bool component = false;
                    bool componentHasBody = false;

                    if (Current == '<') //its a component
                    {
                        component = true;
                        componentHasBody = ParseMacroComponent(ref macroName);
                        if (componentHasBody)
                        {
                            arguments.Add("$body");
                        }
                        //get the arguments
                    }
                    else //if its a normal define
                    {
                        doubleDefine = ParseMacroNormal(ref macroName);
                        //get the arguments
                    }

                    //get all arguments
                    ParseMacroArguments(ref arguments, doubleDefine);
                    while (Current == ' ') //skip all spaces
                    {
                        Position++;
                    }
                    if (component)
                    {
                        while (char.IsWhiteSpace(Current)) //skip all spaces
                        {
                            Position++;
                        }
                        while (Current == '{') Position++; //skip opening brackets

                        while (!(Current == '}' && Peek(1) == '}')) //the body
                        {
                            ParseMacroBody(arguments, ref macroBody);
                        }
                        Position++; //skip first }
                        Position++; //skip second }
                    }
                    else
                    {
                        while (Current != '\n' && Current != '\r') //the body
                        {
                            ParseMacroBody(arguments, ref macroBody);
                        }
                    }

                    var finalMacro = new Macro()
                    {
                        name = macroName,
                        arguments = arguments,
                        body = macroBody,
                        DoubleDefined = doubleDefine,
                        Component = component,
                        ComponentHasBody = componentHasBody,
                    };

                    if (!Macros.Contains(finalMacro))
                        Macros.Add(finalMacro);
                    continue;
                }
                else
                {
                    bool found = false;
                    // Expand normal macros
                    foreach (var macro in Macros.Where(m => !m.Component))
                    {
                        if (Find($"@{macro.name}("))
                        {
                            List<string> arguments = new List<string>();

                            CaptureNormalMacroArguments(macro.DoubleDefined, ref arguments);

                            ApplyMacro(macro, arguments, ref FinalText, ref found);
                            break;
                        }
                    }

                    // Expand component macros
                    foreach (var macro in Macros.Where(m => m.Component))
                    {
                        if (Find($"<{macro.name}"))
                        {
                            List<string> arguments = new List<string>();

                            //pre fill the list of arguments to all be empty
                            for (int i = 0; i < macro.arguments.Count(); i++)
                            {
                                arguments.Add(string.Empty);
                            }

                            CaptureComponentMacroArguments(macro, ref arguments);



                            ApplyMacro(macro, arguments, ref FinalText, ref found);
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
        
        public (string FinalText, string FinalCode) Parse(string input)
        {
            string FinalText = string.Empty;
            string FinalCode = string.Empty;
            this.Analizable = input.ToList();
            bool isFlang = false;
            bool isFPrint = false;


            bool usePrintedHtml = input.Split("\n").Any(l => l.Trim().StartsWith("#use printedHtml"));
            if (usePrintedHtml)
            {
                input = input.Replace("#use printedHtml", "");
                this.Analizable = input.ToList();
                
                FinalCode += "POSITION = 0; print(\"";
            }

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
                if (!input.Contains("<(clientFlang"))
                {
                    throw new System.Exception("#use clientFlang is specified but no \"<(clientflang\" code entry can be found, which means the implementations are missing");
                }
                input = input.Replace("#use clientFlang", "");
                this.Analizable = input.ToList();
                input = ClientCodePreParser(input);
                this.Analizable = input.ToList();
                this.Position = 0;
            }
            else
            {
                if (input.Contains("<(clientflang"))
                {
                    throw new System.Exception("\"<(clientflang\" code entry has been found, but clientFlang isnt enabled, add \"#use clientFlang\" to the top of your file to enable.");
                }
            }

            //debugging
            //File.WriteAllText("compiled.flang", input);

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
                        if (usePrintedHtml)
                        { 
                            FinalCode += " print(\"";
                        }
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
                        if (usePrintedHtml)
                        { 
                            FinalCode += "\");";
                        }
                        isFlang = true;
                        //FinalText += Current;
                        Position++;
                        FinalCode += $" POSITION = {FinalText.Length}; ";
                        continue;
                    }
                    if (Find("<(="))
                    {
                        if (usePrintedHtml)
                        { 
                            FinalCode += "\");";
                        }
                        isFlang = true;
                        isFPrint = true;
                        //FinalText += Current;
                        //Position++;
                        FinalCode += $" POSITION = {FinalText.Length}; print(";
                        continue;
                    }
                    else
                    {
                        if (usePrintedHtml)
                        {
                            if (Current == '\"') FinalCode += "\\";
                            FinalCode += Current;
                        }
                        else
                        { 
                            FinalText += Current;
                        }
                        Position++;
                    }
                }
            }
            if (usePrintedHtml)
            { 
                if (FinalCode.EndsWith(" print(\""))
                {
                    FinalCode = FinalCode.Substring(0, FinalCode.Length - "print(\"".Length);
                }
                else
                {
                    FinalCode += "\");";
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
