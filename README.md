# FlangApache
FlangApache brings the flang "programming language" to the web in a more official way

now you can host `.flang` files on any apache webserver

because i dont know how to write apache modules 
i have done a workaround which works by redirecting all `.flang` files to the following `index.php`
and that php file will then run the parser and return the output 

# how to use
build the project, either as release or publish it to a single file for linux
store this file somewhere on the webserver
add the following file somewhere accessable

`index.php`
```
<?php
$path = ltrim($_SERVER['DOCUMENT_ROOT'], '/').$_SERVER['REQUEST_URI'];

$page = base64_encode(basename($path));
$url = base64_encode($_SERVER['REQUEST_URI']);
$content = base64_encode(file_get_contents('php://input'));
$get = base64_encode(json_encode($_GET));
$post = base64_encode(json_encode($_POST));

$pathEncoded = base64_encode($path);

//for debugging enable this so you can copy these values and just run the project instead
//if (isset($_GET['csDebug']) || isset($_GET['csdebug']))
//    $test = "\"--$pathEncoded\",<br/>\"--$page\",<br/>\"--$url\",<br/>\"--$content\",<br/>\"--$get\",<br/>\"--$post\",";
//else
    $test = shell_exec("C:\\Users\\USERNAME\\source\\repos\\FlangWebsiteCore\\FlangWebsiteConsole\\bin\\Release\\net7.0\\FlangWebsiteConsole.exe --$pathEncoded --$page --$url --$content --$get --$post");


echo $test;

?>
```
i stored the index at
`C:/xampp/htdocs/tools/flang/index.php`

and then add the following to your `httpd.conf` file
```
Alias /tools "C:/xampp/htdocs/tools/"

<FilesMatch "\.flang$">
    SetHandler application/x-httpd-php
    RewriteEngine On
    RewriteRule ^(.*)$ /tools/flang/index.php [QSA,L]
</FilesMatch>
```
thanks to matt for the code which helped me greatly
https://stackoverflow.com/a/33317978

restart apache and everything should work (i think [i heavent tried it on linux yet])

for examples check out the testing folder and then examples
examples is most up to date the old folder may be out of date but most in old should work tools

but the basic gist is use `flang context` to start using flang 
basic flang context:
```
<(flang
	print("Hello world");
)>
```
or if you just want to print something instead of using it fully you can DOCUMENT_ROOT
```
<(="Hello world")>
```

and you would get the same result
here is a basic example
`readme.flang`
```
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Flang test</title>
</head>
<body>
    <(flang
        for (int i = 1; i <= 10; i++)
        {
            print("num "+i);
            print("</br>");
        }

        //use localhost/readme.flang?message=hello world
        var message = GET["message"];
    )>
    <p>
        the message was: <(=message)>
    </p>
</body>
</html>
```

it shows basic for loop and the printing system, also how to use GET to get stuff from the GET request/url
the default varibles you have access to are
- PAGE
- URL
- CONTENT
- GET
- POST

for our little example the values would be:
PAGE=`readme.flang?message=hello%20world`
URL=`...flangapi/examples/readme.flang?message=hello%20world`
CONTENT=``
GET=`{ message: hello world }`
POST=`{ }`

those are the basics but there are also macros
```
#define printLine($msg)
{{
	print($msg);
	print("</br>");
}}
```
wich you can use (in flang context only) like this:
`@printLine("Hello world")`

you can also define macro components
```
#define <CoolDiv>($title)
{{
	<p>$title</p>
	<div>
		$body
	</div>
}}
```
when defining a component macro with a body the argument `$body` is the body and will be added automatically
use like this (in html context only)
```
<CoolDiv $title="Cool title">
	Cool body
</CoolDiv>
```

if you dont want a body on your component macro you can define it like this instead adding a `/` in the definition
```
#define <CoolDiv/>($title)
{{
	<p>$title</p>
	<div>
		Cool body since no body argument is passed down
	</div>
}}
```

and then you would call it like this
`<CoolDiv $title="Cool title" />`

flang code runs on the server NEVER on the client
but flang has auto-api to abstract alot and make it SEEM like it runs on the client
instead of `<(flang` you use `<(clientFlang`
but you also have to opt in and put `#use clientFlang` and add `<$clientEventHandeler$>` in the head somewhere

here is basic example of reading and writing to a text file using clientFlang
```
<!-- opt in to clientFlang and enable the usage macros, 
a few predefined macros
@setBind prepare var to be binded
@bind make input bind varible
@get document.getElementById
@set innerText =
-->
#use clientFlang 
#use macros

<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>clientFlang readme</title>
    <!-- the client code needs an entry point to put script tags ig generates you can define where it puts them -->
    <$clientEventHandeler$>
    <script>
        //declare and prepare input to be binded
        var input;
        @setBind(input);
    </script>
</head>
<body>
    <!-- whenever the input changes itll update the input varible -->
    <input @bind(input)/>
    <!-- on this button click call client method CLIENT_writeFile -->
    <button onclick="<(@CLIENT_writeFile)>(input)">Write to file</button>
    <br/>
    <p>text is:</p> 
    <!-- client code is also availiby from server
     so we call the same readfile on startup to have initial value -->
    <p id="text"><(=CLIENT_readFile())></p>

    <!-- when we update we have to update the element so we call js method first -->
    <button onclick="readFile()">update</button>
    <script>
        async function readFile()
        {
            //we get the element and call the client method CLIENT_readFile wich we have to await
            var field = @get("text");
            var content = await <(@CLIENT_readFile)>();
            field.@set(content);
        }
    </script>
</body>
</html>

<!-- clientFlang code wich runs on server but gets auto api implemented for it
 so it can be called easily from client with a ton of abstractions
    call like this <(@METHODNAME)>(javascriptVaribleName)
  -->
<(clientFlang
    void clientFlang CLIENT_writeFile(string text)
    {
        File.write("log.txt", text);
    }
    string clientFlang CLIENT_readFile()
    {
        return File.read("log.txt");
    }
)>
```
we normally cant write files from client and flang cant even run client-side so we have these auto-api's that can do it.


in newer versions i have also added `calls` wich do even more abstraction 
and also handle the the html element and generates a bit more javascript

a call generates a clientEvent and an htmlElemnt to hold the result of the event/call

instead of reading a file and displaying the output like this:
```
	<button onclick="readFile()">Read text</button>
	<p id="output"><(=readFile())></p>
	
	<script>
		async function readFile()
		{
			var content = await <(@readFile)>();
			var outputElement = @get("output");
			outputElement.@set(content);
		}
	</script>
	
	<(clientFlang
		string clientFlang readFile()
		{
			return File.read("log.txt");
		}
	)>
```

you can do it like this using calls:
```
	<button onclick="@call readFile2()">Read text2</button>
	
	@oncall readFile2()
	<(default: @call readFile2())>
	<{
		return File.read("log.txt");
	}>
```

this is pretty much syntax suger because it pretty much generates the code above
but it definitly makes it much easier


to define a call you use 
`@oncall ExampleCall(string name)`
followed by the body of the call
```
<{
	var lastName = name.split(" ").last();
	return "<div>your last name is:{lastName}</div>"$;
}>
```

the `@put` and `@withValue` macros also exist for these calls to make returning easier 
and is the prefferd syntax for when you want to return more html
`@put((anything here))` = `return {{raw{{ {0} }}raw}}`
`@withValue(this, isThis)` = `.replace({0},{1})`

```
<{
	var lastName = name.split(" ").last();
	@put((
		<div>
			your last name is:{lastName}
		</div>
	)).@withValue("{lastName}",lastName);
}>
```

for calls you also have a few extra things
like its default value
<(default: call hasnt been called yet, awaiting value)>
normally this is just plaintext
however if you prefix it with `@call`
its still plain text but gets put inbetween `<(=` and `)>`
making it so you can use flang again
so if you want its default value to be the result you can call it initialy like this
<(default: @call ExampleCall("jhonn do"))>
if you want to execute javascript before the actual call happens use 
`<(javascript:alert("i have been called"))>` or `<(jsbefore:alert("i have been called"))>`
and if you want javascript after use 
`<(jsafter:alert("i had been called"))>`
if you want some value WHILE its loading use
`<(loading:loading...)>` or `<(load:please wait..)>`


here is an example of a (insecure) account system using flang calls

```
#use macros
#use clientFlang
#use call

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>login</title>
    <script>
        var username;
        @setBind(username);

        var password;
        @setBind(password);

        var loginMode = false;
    </script>
    <$clientEventHandeler$>
    <(clientFlang
        string database = "accounts.txt";
        void CheckFolders()
        {
            if (!File.exists(database))
            {
                File.write(database, "admin:password\n");
            }
        }
    )>
</head>
<body>
    <div>
        <p>Welcome to account</p>
        <p>username:</p>
        <input @bind(username)/>
        <p>password:</p>
        <input @bind(password)/>
    </div>
    <br/>
    <button onclick="@call Register(username, password)">Register</button>
    <br/>
    <button onclick="@call Login(username, password)">Login</button>


    @oncall Register(string username, string password)
    <(default:<p style="opacity:.5;">Not registerd</p>)>
    <(load:<p style="opacity:.6;">Checking username..</p>)>
    <{
        CheckFolders();
        bool exists = false;

        var lines = File.readLines(database);
        foreach (var line in lines)
        {
            if (line.startsWith(username+":"))
            {
                exists = true;
                break;
            }
        }
        if (exists)
        {
            @put((
                <p style="color:red;">Account already exists</p>
            ));
        }
        else
        {
            File.append(database, "{username}:{password}\n"$);
            @put((
                <p style="color:green;">Account created</p>
            ));
        }
    }>

    @oncall Login(string username, string password)
    <(default:<p style="opacity:.5;">Not logged in</p>)>
    <(load:<p style="opacity:.6;">Checking username..</p>)>
    <{
        CheckFolders();
        bool exists = false;
        string existingPassword = "";

        var lines = File.readLines(database);
        foreach (var line in lines)
        {
            if (line.startsWith(username+":"))
            {
                existingPassword = line.split(":").last();
                exists = true;
                break;
            }
        }
        if (exists)
        {
            if (password == existingPassword)
            {
                @put((
                    <p style="color:green;">Login successful!</p>
                ));
            }
            else
            {
                @put((
                    <p style="color:red;">username or password is incorrect</p>
                ));
            }
        }
        else
        {
            @put((
                <p style="color:red;">username or password is incorrect</p>
            ));
        }
    }>
</body>
</html>
```
