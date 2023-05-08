#!/usr/bin/sh
ver=$(chromium --version | grep -oE '([0-9]+[.]){2,}[0-9]+')
echo "Chrome version: $ver"
dotnet add package Selenium.WebDriver.ChromeDriver --version "$ver"
