$ErrorActionPreference = 'Stop'
Add-Type -Path 'D:\Hack the Hill\Deliverables\TherapyGame\Runtime\WellnessCaptions.cs'
$captionState = [TheLastWatch.Integrations.WellnessCaptions]::new()
$script:captionAssertions = 0
function Assert-Caption([bool]$condition,[string]$message) {
    $script:captionAssertions++
    if (!$condition) { throw "Caption check failed: $message" }
}
Assert-Caption ($captionState.Message -eq '...') 'initial waiting ellipsis'
$captionState.Agent('A gentle thought.', 15, 1)
$captionState.Tick($true,$false,$false,1.2)
Assert-Caption ($captionState.Who.ToString() -eq 'Companion' -and $captionState.Message -eq 'A gentle thought.') 'actual companion caption while speaking'
$captionState.Correct('A corrected thought.',15,1.3)
Assert-Caption ($captionState.Message -eq 'A corrected thought.') 'matching correction'
$captionState.Correct('Stale text',14,1.4)
Assert-Caption ($captionState.Message -eq 'A corrected thought.') 'stale correction ignored'
$captionState.Tick($false,$false,$false,4)
Assert-Caption ($captionState.Message -eq '...') 'bot waiting for response'
$captionState.Tick($false,$true,$false,5)
Assert-Caption ($captionState.Who.ToString() -eq 'Listening') 'speech detected before final transcript'
$captionState.User('Today felt calmer.',16,5.5)
$captionState.Tick($false,$true,$false,5.6)
Assert-Caption ($captionState.Who.ToString() -eq 'Player' -and $captionState.Message -eq 'Today felt calmer.') 'user words in same caption channel'
$captionState.Tick($false,$false,$false,7)
Assert-Caption ($captionState.Message -eq 'Today felt calmer.') 'user caption remains readable'
$captionState.Agent('Thank you for sharing.',17,8)
$captionState.Tick($true,$false,$false,8.1)
Assert-Caption ($captionState.Who.ToString() -eq 'Companion') 'next bot turn replaces player turn'
$captionState.Interrupt(8.2)
Assert-Caption ($captionState.Message -eq '...') 'interrupted bot words cleared'
$captionState.Tick($false,$true,$true,9)
Assert-Caption ($captionState.Message -eq '...') 'muted voice cannot show false listening'
$captionState.Agent(('A long caption with real words. ' * 25),18,10)
Assert-Caption ($captionState.Pages.Length -gt 3) 'long captions paginated'
foreach ($page in $captionState.Pages) { Assert-Caption ($page.Length -le 140) 'bounded page length' }
$captionState.Tick($true,$false,$false,20)
Assert-Caption ($captionState.Page -eq 1) 'long caption advances'
$captionState.Reset()
Assert-Caption ($captionState.Message -eq '...' -and $captionState.Pages.Length -eq 1 -and $captionState.Page -eq 0) 'disconnect reset removes old words'
$unicode = ('a' * 139) + [char]::ConvertFromUtf32(0x1F331) + ' garden'
$pages = [TheLastWatch.Integrations.WellnessCaptions]::Split($unicode,140)
Assert-Caption (![char]::IsHighSurrogate($pages[0][$pages[0].Length-1])) 'emoji not split across pages'
$literal = '<b>This is literal transcript text.</b>'
$captionState.User($literal,20,21)
Assert-Caption ($captionState.Message -eq $literal) 'caption content remains literal data'
$captionState.Tick($false,$false,$false,30)
Assert-Caption ($captionState.Message -eq '...') 'idle returns to ellipsis'
Write-Output "PASS: $script:captionAssertions caption-state assertions. No Unity, microphone, network or graphics session started."
