#/usr/bin/sh
set -e
cd $(dirname "$0")

SRVPID=$(netstat -pl | awk '/8000.*LISTEN.*python/ { sub(/[/]python/, "", $7); print $7 }')
if [ ! -z "$SRVPID" ]
then
    kill "$SRVPID"
fi

python -m http.server
