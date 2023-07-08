#!/bin/sh

set -e

URLS='http://testhost:8000/angular.html \
      http://testhost:8000/arg-alpha-dot-bt.html?a=1 \
      http://testhost:8000/arg-alpha-paren.html?a=1 \
      http://testhost:8000/arg-cookie-exec.html?a=1 \
      http://testhost:8000/arg-cookie-refl.html?a=1 \
      http://testhost:8000/arg-write-alnum.html?a=1 \
      http://testhost:8000/arg-write.html?a=1 \
      http://testhost:8000/boring-js.html \
      http://testhost:8000/boring-js.html?a=1 \
      http://testhost:8000/frag-eval.html?a=1 \
      http://testhost:8000/frag-timeout.html?a=1 \
      http://testhost:8000/static.html \
      http://testhost:8000/vue2-comp.html \
      http://testhost:8000/vue2.html \
      http://testhost:8000/vue3-comp.html \
      http://testhost:8000/vue3.html'

rm -rf wget-tmp
wget --no-warc-compression --recursive --quiet --output-document=wget-tmp --warc-file=test $URLS
rm -rf wget-tmp
