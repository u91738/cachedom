# cachedom
Tool to look for DOM XSS with lots of caching and minimal server load.

running as
```shell
cachedom --url http://example.com?a=123
```
will make cachedom go to `http://example.com?a=123`, cache every HTTP request,
then try XSS payloads in parameters found in url (`a=...`) and url fragment (`http://example.com?a=123#...`)
in hope of finding a DOM XSS vulnerability with minimal load on the server.
If new parameters make the page create network requests that are not in cache - they will go to the server.

For more options
```shell
cachedom --help
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
