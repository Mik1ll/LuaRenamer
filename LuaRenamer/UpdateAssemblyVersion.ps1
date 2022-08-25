function getVersion()
{
    $tag = iex "git describe --match 'v[0-9]*' --long --tags --always"
    $a = [regex]"v\d+\.\d+\.\d+\-\d+"
    $b = $a.Match($tag)
    $b = $b.Captures[0].value
    $b = $b -replace '-', '.'
    $b = $b -replace 'v', ''
    Write-Host "Version found: $b"
    return $b
}


function SetVersion ($version)
{
    $file = "AssemblyInfo.cs"
    "Changing version in $file to $version"
    $fileObject = get-item $file

    $sr = new-object System.IO.StreamReader( $file, [System.Text.Encoding]::GetEncoding("utf-8") )
    $content = $sr.ReadToEnd()
    $sr.Close()

    $content = [Regex]::Replace($content, "(\d+)\.(\d+)\.(\d+)[\.(\d+)]*", $version);

    $sw = new-object System.IO.StreamWriter( $file, $false, [System.Text.Encoding]::GetEncoding("utf-8") )
    $sw.Write( $content )
    $sw.Close()
}

# First get tag from Git
$version = getVersion
Setversion $version