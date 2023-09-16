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

//echo "you are visiting $path";

$page = base64_encode(basename($path));
$url = base64_encode($_SERVER['REQUEST_URI']);
$content = base64_encode(file_get_contents('php://input'));
$get = base64_encode(json_encode($_GET));
$post = base64_encode(json_encode($_POST));

$test = shell_exec("C:\\Users\\USERNAME\\source\\repos\\FlangWebsiteCore\\FlangWebsiteConsole\\bin\\Release\\net7.0\\FlangWebsiteConsole.exe --$path --$page --$url --$content --$get --$post");
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
