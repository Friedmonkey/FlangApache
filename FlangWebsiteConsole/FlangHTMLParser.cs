using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;

namespace FlangWebsiteConsole
{
    public class FlangHTMLParser : AnalizerBase<char>
    {
        public FlangHTMLParser() : base('\0') { }
        private string ClientCodePreParser(string input)
        {
            string FinalText = string.Empty;

            bool isClientFlang = false;
            string FinalClientCode = string.Empty;
            List<string> clientFunctions = new List<string>();

            while (Current != '\0')
            {
                if (isClientFlang)
                {
                    if (Find(")>"))
                    {
                        isClientFlang = false;
                        continue;
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
                        FinalText += functionName + "()";

                        if (!clientFunctions.Contains(functionName))
                            clientFunctions.Add(functionName);
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
            foreach (var functionName in clientFunctions)
            {
                clientEventHandeler += $$"""
            	async function {{functionName}}()
            	{
            		<(flang
            			if (URL.contains("?"))
            				print("return (await (await fetch(\"{URL}&clientEvent={{functionName}}\")).text());"$);
            			else
            				print("return (await (await fetch(\"{URL}?clientEvent={{functionName}}\")).text());"$);
            		)>
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
            foreach (var functionName in clientFunctions)
            {
                output += $$"""
                    if (GET["clientEvent"] == "{{functionName}}")
                    {
                        var output = {{functionName}}();
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
        public (string FinalText, string FinalCode) Parse(string input)
        {
            string FinalText = string.Empty;
            string FinalCode = string.Empty;
            this.Analizable = input.ToList();
            bool isFlang = false;
            bool isFPrint = false;

            bool useClient = input.Split("\n").Any(l => l.Trim().StartsWith("#clientFlang"));
            if (useClient)
            {
                if (!input.Contains("<$clientEventHandeler$>"))
                {
                    throw new System.Exception("clientEventHandeler is missing, client code cannot be executed, \"<$clientEventHandeler$>\" in the head of your html file");
                }
                if (!input.Contains("<(clientflang"))
                {
                    throw new System.Exception("#clientFlang is specified but no \"<(clientflang\" code entry can be found, which means the implementations are missing");
                }
                input = input.Replace("#clientFlang", "");
                this.Analizable = input.ToList();
                var output = ClientCodePreParser(input);
                this.Analizable = output.ToList();
                this.Position = 0;
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
