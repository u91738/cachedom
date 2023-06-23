# cachedom
Tool to look for DOM XSS with lots of caching and minimal server load.

running as `cachedom --url http://example.com?a=123` will make cachedom go to `http://example.com?a=123`, cache every HTTP request,
then try XSS payloads in parameters found in url (`a=...`) and url fragment (`http://example.com?a=123#...`)
in hope of finding a DOM XSS vulnerability with minimal load on the server.

If new parameters make the page create network requests that are not in cache - they will go to the server.

If no payloads in config file cause js execution, will check what charsets from argument will get into page body, cookies, log or js calls like write and eval.

For more options
```
$ cachedom --help
cachedom - search for DOM XSS with limited interaction with server
Usage:
    cachedom --url http://example.com?a=123\n\
OPTIONS
(See default.json for defaults)
-u URL, --url URL
    url to check, pass multiple --url ... if needed
-c configFile, --config configFile
    path to config file. See default.json
-s, --check-sub-urls
    check urls used in subrequests of what was passed in --url ... (AJAX, frames)
-p PORT, --proxy-port PORT
    port to use for internal proxy server
--js-body-filter, --no-js-body-filter
    only check pages if they have on...=, javascript, <script etc in their body
--show-browser
    do not run browser in headless mode, mostly a debug feature
--ignore-cookies
    do not try to put payloads into cookies. Can save a lot of time.
--cache-mode [precise|strip-arg-values|strip-arg-names-values]
    precise
        respond from cache to requests that fully match previous request in cache
    strip-arg-values
        respond from cache to requests that have different arg value
        i.e. reply with response from http://example.com/some/page?a=123
        to request for http://example.com/some/page?a=abc
    strip-arg-names-values
        respond from cache to requests that have different arg name and value
        i.e. reply with response from http://example.com/some/page?a=123
        to request for http://example.com/some/page?x=abc
```

## Usage examples

Find an XSS exploitable by a payload in config
```
$ cachedom --url http://10.0.0.1:8000/arg-write.html?a=1
...
Check url: http://10.0.0.1:8000/arg-write.html?a=1
GET parameter 0 leads to JS execution in Url:
http://10.0.0.1:8000/arg-write.html?a=<script>console.log`1233321`</script>
```

Find argument reflections to body and argument passed to document.write() when it is not proven to be exploitable
```
$ cachedom --url http://10.0.0.1:8000/arg-write-alnum.html?a=1
...
Check url: http://10.0.0.1:8000/arg-write-alnum.html?a=1
Body:
    Lower http://10.0.0.1:8000/arg-write-alnum.html?a=abctestcba

Instr document_write:
    Lower http://10.0.0.1:8000/arg-write-alnum.html?a=abctestcba
    call: document_write ( abctestcba )
exception stack:
    Error
        at obj_desc (http://10.0.0.1:8000/arg-write-alnum.html?a=abctestcba:68:25)
        at obj.<computed> [as write] (http://10.0.0.1:8000/arg-write-alnum.html?a=abctestcba:98:59)
        at http://10.0.0.1:8000/arg-write-alnum.html?a=abctestcba:132:22

Body:
    Upper http://10.0.0.1:8000/arg-write-alnum.html?a=ABCTESTCBA

Instr document_write:
    Upper http://10.0.0.1:8000/arg-write-alnum.html?a=ABCTESTCBA
    call: document_write ( ABCTESTCBA )
exception stack:
    Error
        at obj_desc (http://10.0.0.1:8000/arg-write-alnum.html?a=ABCTESTCBA:68:25)
        at obj.<computed> [as write] (http://10.0.0.1:8000/arg-write-alnum.html?a=ABCTESTCBA:98:59)
        at http://10.0.0.1:8000/arg-write-alnum.html?a=ABCTESTCBA:132:22

Body:
    Numeric http://10.0.0.1:8000/arg-write-alnum.html?a=321123

Instr document_write:
    Numeric http://10.0.0.1:8000/arg-write-alnum.html?a=321123
    call: document_write ( 321123 )
exception stack:
    Error
        at obj_desc (http://10.0.0.1:8000/arg-write-alnum.html?a=321123:68:25)
        at obj.<computed> [as write] (http://10.0.0.1:8000/arg-write-alnum.html?a=321123:98:59)
        at http://10.0.0.1:8000/arg-write-alnum.html?a=321123:132:22
```
