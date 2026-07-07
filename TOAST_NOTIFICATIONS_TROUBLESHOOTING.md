# Toast Notifications Troubleshooting Guide

## ❌ ISSUE: Toast Notifications Not Appearing

If you're not seeing Windows toast notifications when running operations, here's a complete troubleshooting guide.

---

## 🔍 WHAT WAS FIXED IN BUILD 1.0.8

### 1. ✅ AppUserModelID Registration

**Problem:** WPF applications need to register an AppUserModelID to show toast notifications.

**Fix Applied:**
```csharp
// In App.xaml.cs
SetCurrentProcessExplicitAppUserModelID("FileOrganizer.PortableFileOrganizer.v5");
```

### 2. ✅ Proper Toast API Usage

**Fix Applied:**
```csharp
// Use the AppId when creating notifier
var notifier = ToastNotificationManager.CreateToastNotifier(AppId);
notifier.Show(toast);
```

### 3. ✅ Better Error Logging

Added debug logging to help identify issues:
```csharp
System.Diagnostics.Debug.WriteLine($"[Toast] Sent: {operationName} Started");
```

### 4. ✅ Test Notification Button

Added "Test Toast Notifications" button in Help tab to verify functionality.

---

## 🧪 HOW TO TEST

### Step 1: Use the Test Button
1. Open the application
2. Go to **Help** tab
3. Click **"🔔 Test Toast Notifications"** button
4. Check if a notification appears

### Step 2: Check Action Center
1. Click the notification icon in Windows taskbar (bottom-right)
2. Look for "Test Notification" from "Portable File Organizer"

### Step 3: Run a Real Operation
1. Go to **Configuration** tab
2. Select a source folder
3. Click **"🔍 Initial Scan"**
4. Watch for "Initial Scan Started" notification

---

## ⚙️ WINDOWS SETTINGS TO CHECK

### 1. Verify Notifications Are Enabled

**Windows 11:**
```
Settings → System → Notifications
→ Ensure "Notifications" toggle is ON
→ Scroll down to "Notifications from apps and other senders"
→ Find "Portable File Organizer" (or "PortableFileOrganizer.exe")
→ Ensure it's enabled
```

**Windows 10:**
```
Settings → System → Notifications & actions
→ Ensure "Get notifications from apps and other senders" is ON
→ Scroll down and find "Portable File Organizer"
→ Ensure it's enabled
```

---

### 2. Check Focus Assist Settings

**What Is Focus Assist?**
Focus Assist (formerly "Quiet Hours") can block notifications.

**Windows 11:**
```
Settings → System → Focus assist
→ Set to "Off" (to see all notifications)
→ Or check "Priority only" list includes your app
```

**Windows 10:**
```
Settings → System → Focus assist
→ Set to "Off" or "Priority only"
→ Check priority list includes the app
```

---

### 3. Verify Windows Version

**Minimum Required:**
- Windows 10 version 1809 (October 2018 Update)
- Build 17763 or later

**Check Your Version:**
```
Press Win + R
Type: winver
Press Enter
→ Should show "Version 1809" or higher
```

---

## 🛠️ COMMON ISSUES & FIXES

### Issue 1: No Notifications at All

**Symptoms:**
- Test button shows success but no notification appears
- No errors in debug output

**Possible Causes:**
1. Notifications disabled in Windows Settings
2. Focus Assist blocking notifications
3. Action Center disabled
4. App not in allowed list

**Fix:**
1. Open Windows Settings → System → Notifications
2. Enable notifications globally
3. Find "Portable File Organizer" and enable it
4. Disable Focus Assist temporarily
5. Restart the application

---

### Issue 2: Notifications Work Once Then Stop

**Symptoms:**
- First notification appears
- Subsequent notifications don't appear

**Possible Cause:**
Windows notification spam protection

**Fix:**
1. Open Action Center
2. Clear old notifications
3. Restart the application
4. Try again

---

### Issue 3: Test Notification Fails

**Symptoms:**
- Dialog shows "Failed to send test notification"
- Error in debug output

**Possible Causes:**
1. Windows version too old (< 1809)
2. Missing Windows SDK components
3. Notification service not available

**Fix:**
1. Update Windows to latest version
2. Check Windows Update for pending updates
3. Run: `Get-AppxPackage *notifications* | ForEach {Add-AppxPackage -DisableDevelopmentMode -Register "$($_.InstallLocation)\AppXManifest.xml"}`

---

### Issue 4: Notifications Appear But No Sound

**Symptoms:**
- Notifications appear silently
- No notification sound plays

**Fix:**
1. Windows Settings → System → Notifications
2. Click on "Portable File Organizer"
3. Enable "Play a sound when a notification arrives"
4. Check Windows volume mixer

---

### Issue 5: Notifications Go to Action Center But Don't Pop Up

**Symptoms:**
- Notifications in Action Center
- No popup/banner on screen

**Fix:**
1. Windows Settings → System → Notifications
2. Find "Portable File Organizer"
3. Ensure "Show notification banners" is enabled
4. Set priority to "Top" or "High"

---

## 🔬 ADVANCED DEBUGGING

### Enable Debug Output

If running from Visual Studio or with debugger attached, check **Output** window for:

```
[Toast] Sent: Initial Scan Started
[Toast] Sent: Initial Scan Completed in 5m 32s
```

If you see errors like:
```
[Toast] Failed to show notification: [error message]
```

This indicates the actual error preventing notifications.

---

### Check Event Viewer

1. Open Event Viewer (Win + X → Event Viewer)
2. Navigate to: Windows Logs → Application
3. Look for warnings/errors from "PortableFileOrganizer.exe"
4. Check for notification-related errors

---

### Run as Administrator

Sometimes notification permissions require elevated privileges:

1. Right-click PortableFileOrganizer.exe
2. Select "Run as administrator"
3. Try test notification again

---

## 📋 COMPLETE VERIFICATION CHECKLIST

Use this checklist to verify your setup:

### System Requirements
- [ ] Windows 10 version 1809 or later (check with `winver`)
- [ ] Latest Windows updates installed
- [ ] .NET 9.0 runtime (included in portable build)

### Windows Settings
- [ ] Notifications enabled globally (Settings → System → Notifications)
- [ ] "Portable File Organizer" in allowed apps list
- [ ] Notification banners enabled for app
- [ ] Sound enabled for notifications (if desired)
- [ ] Focus Assist set to "Off" or app in priority list

### Application Settings
- [ ] Test button in Help tab shows success
- [ ] Test notification appears in Action Center
- [ ] Scan operations trigger notifications
- [ ] Notifications have correct text and duration

### Troubleshooting Steps Tried
- [ ] Restarted application
- [ ] Restarted Windows
- [ ] Cleared Action Center
- [ ] Checked Event Viewer for errors
- [ ] Verified Windows version
- [ ] Updated Windows to latest version

---

## 🎯 EXPECTED BEHAVIOR

When toast notifications are working correctly:

### Initial Scan
**Start Notification:**
```
┌─────────────────────────────────┐
│ Initial Scan Started            │
│ Scanning C:\Users\...           │
│ Portable File Organizer         │
└─────────────────────────────────┘
```

**Completion Notification:**
```
┌─────────────────────────────────┐
│ Initial Scan Completed          │
│ Found 1,234 files               │
│ Duration: 5m 32s                │
│ Portable File Organizer         │
└─────────────────────────────────┘
```

### Live Move
**Start:**
```
┌─────────────────────────────────┐
│ Live Move Started               │
│ Moving 1,234 files to D:\...    │
│ Portable File Organizer         │
└─────────────────────────────────┘
```

**Completion:**
```
┌─────────────────────────────────┐
│ Live Move Completed             │
│ Moved 1,234/1,234 files (5.67 GB)│
│ Duration: 8m 45s                │
│ Portable File Organizer         │
└─────────────────────────────────┘
```

---

## 🚫 IF NOTHING WORKS

If you've tried everything and notifications still don't work:

### Option 1: Use Without Notifications
The application works perfectly without toast notifications. You'll still get:
- ✅ Status messages in the app
- ✅ Duration tracking in status bar
- ✅ Progress bars during operations
- ✅ Completion dialogs with results
- ✅ All core functionality

### Option 2: Alternative Notification Methods

**Taskbar Flash:**
The app window flashes in the taskbar when operations complete (Windows default behavior).

**Sound Notifications:**
Windows plays system sounds for operation completion (if enabled).

**Always-on-Top:**
Keep the application window visible while working to see progress in real-time.

---

## 📊 NOTIFICATION COMPARISON

| Notification Method | Shows When Minimized | Requires Configuration | User Experience |
|---------------------|----------------------|------------------------|-----------------|
| **Toast Notifications** | ✅ Yes | Some setup | Best |
| **Taskbar Flash** | ✅ Yes | None | Good |
| **System Sounds** | ✅ Yes | None | OK |
| **In-App Status** | ❌ No | None | Basic |
| **Completion Dialogs** | ⚠️ May steal focus | None | Interrupting |

---

## 🆘 STILL NEED HELP?

### Debug Information to Collect

If reporting an issue, please provide:

1. **Windows Version:**
   - Run `winver` and share version number

2. **Application Version:**
   - Help tab → Shows "v5.0 build 1.0.8"

3. **Test Result:**
   - Help tab → Click "Test Toast Notifications"
   - Share the exact error message

4. **Debug Output:**
   - If running with debugger, copy output from:
   ```
   [Toast] lines in Output window
   ```

5. **Notification Settings:**
   - Screenshot of Settings → System → Notifications
   - Show "Portable File Organizer" settings

6. **Event Viewer:**
   - Any errors from Event Viewer → Application log

---

## ✅ SUMMARY

**What Should Happen:**
1. App registers AppUserModelID on startup
2. Operations send toast notifications via Windows API
3. Notifications appear as banners (if enabled)
4. Notifications saved to Action Center
5. User gets instant feedback on operation status

**What to Check:**
1. Windows version ≥ 1809
2. Notifications enabled in Settings
3. Focus Assist not blocking
4. App in allowed notifications list
5. Test button shows success

**If All Else Fails:**
Use the app without toast notifications - all core features work perfectly!

---

**Document Version:** 1.0.8  
**Last Updated:** March 11, 2026  
**Status:** Complete troubleshooting guide
