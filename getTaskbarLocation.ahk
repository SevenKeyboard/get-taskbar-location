#Requires AutoHotkey v1.1.31+
;==============================================================
; getTaskbarLocation — Gets the Windows taskbar location from StuckRects3 registry settings
;
; GitHub: https://github.com/SevenKeyboard/get-taskbar-location
; Author: SevenKeyboard Ltd. (2026)
; License: The Unlicense
;
; Documentation / References:
;   How to use the taskbar in Windows
;     https://support.microsoft.com/en-us/windows/how-to-use-the-taskbar-in-windows-0657a50f-0cc7-dbfd-ae6b-05020b195b07#WindowsVersion=Windows_10
;   How to Change Taskbar Location on Screen in Windows 10
;     https://www.tenforums.com/tutorials/57280-change-taskbar-location-screen-windows-10-a.html
;==============================================================
class VersionManager_getTaskbarLocation
{
    static _ := VersionManager_getTaskbarLocation._init()
    _init()    {
        global
        GETTASKBARLOCATION_VERSION := "1.0.1"
    }
}
getTaskbarLocation()    {
    try  {
        regRead stuckRects3, HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3, Settings
    }  catch  {
        return "Bottom"
    }
    switch (subStr(stuckRects3,25,2))
    {
        case 0:     return "Left"
        case 1:     return "Top"
        case 2:     return "Right"
        default:    return "Bottom" ;  case 3:
    }
}
/*
getSecondaryTaskbarLocation(n)    {
    ;  HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MMStuckRects3
}
*/