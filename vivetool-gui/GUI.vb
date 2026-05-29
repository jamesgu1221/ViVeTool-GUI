'ViVeTool-GUI - Windows Feature Control GUI for ViVeTool
'Copyright (C) 2022  Peter Strick / Peters Software Solutions
'
'This program is free software: you can redistribute it and/or modify
'it under the terms of the GNU General Public License as published by
'the Free Software Foundation, either version 3 of the License, or
'(at your option) any later version.
'
'This program is distributed in the hope that it will be useful,
'but WITHOUT ANY WARRANTY; without even the implied warranty of
'MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'GNU General Public License for more details.
'
'You should have received a copy of the GNU General Public License
'along with this program.  If not, see <https://www.gnu.org/licenses/>.
Option Strict On
Imports Newtonsoft.Json.Linq
Imports Albacore.ViVe
Imports Albacore.ViVe.NativeEnums
Imports Albacore.ViVe.NativeStructs
Imports System.Runtime.InteropServices
Imports Telerik.WinControls.UI

''' <summary>
''' ViVeTool GUI
''' </summary>
Public Class GUI
    ''' <summary>
    ''' P/Invoke constants
    ''' </summary>
    Private Const WM_SYSCOMMAND As Integer = &H112
    Private Const MF_STRING As Integer = &H0
    Private Const MF_SEPARATOR As Integer = &H800
    Dim LineStage As String = String.Empty

    ''' <summary>
    ''' P/Invoke declaration. Used to Insert the About Menu Element, into the System Menu. Function get's the System Menu
    ''' </summary>
    ''' <param name="hWnd"></param>
    ''' <param name="bRevert"></param>
    ''' <returns></returns>
    <DllImport("user32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function GetSystemMenu(hWnd As IntPtr, bRevert As Boolean) As IntPtr
    End Function

    ''' <summary>
    ''' P/Invoke declaration. Used to Insert the About Menu Element, into the System Menu. Function Appends to the System Menu
    ''' </summary>
    ''' <param name="hMenu"></param>
    ''' <param name="uFlags"></param>
    ''' <param name="uIDNewItem"></param>
    ''' <param name="lpNewItem"></param>
    ''' <returns></returns>
    <DllImport("user32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function AppendMenu(hMenu As IntPtr, uFlags As Integer, uIDNewItem As Integer, lpNewItem As String) As Boolean
    End Function

    ''' <summary>
    ''' Load Event, Populates the Build Combo Box
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub GUI_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        'Make a Background Thread that handles Background Tasks
        Dim BackgroundThread As New Threading.Thread(AddressOf BackgroundTasks) With {
            .IsBackground = True
        }
        BackgroundThread.SetApartmentState(Threading.ApartmentState.STA)
        BackgroundThread.Start()

        'Disable the close button in the search row
        RGV_MainGridView.MasterView.TableSearchRow.ShowCloseButton = False
    End Sub

    ''' <summary>
    ''' Background Tasks to be executed in a Thread
    ''' </summary>
    Private Sub BackgroundTasks()
        'Populate the Build Combo Box, but first check if the PC is connected to the Internet, otherwise the GUI will crash without giving any helpful Information on WHY
        PopulateBuildComboBox_Check()
    End Sub

    ''' <summary>
    ''' Check for Internet Connectivity before trying to populate the Build Combo Box
    ''' </summary>
    Private Sub PopulateBuildComboBox_Check()
        'Add manual option
        Invoke(Sub()
                   RDDL_Build.Items.Add("Load manually...")
                   RemoveHandler RDDL_Build.SelectedIndexChanged, AddressOf PopulateDataGridView
                   AddHandler RDDL_Build.SelectedIndexChanged, AddressOf PopulateDataGridView
               End Sub)

        If CheckForInternetConnection() Then
            'Populate the Build Combo Box
            PopulateBuildComboBox()

            'Set Ready Label
            Invoke(Sub() RLE_StatusLabel.Text = "Ready. Select a build from the Combo Box to get started, or alternatively press F12 to manually change a Feature.")
        Else
            Invoke(Sub()
                       'First, disable the Combo Box
                       RDDL_Build.Enabled = True
                       RDDL_Build.Text = "Network Error"

                       'Second, change the Status Label
                       RLE_StatusLabel.Text = "Network Functions disabled. Press F12 to manually change a Feature."

                       'Third, Show an error message
                       Dim RTD As New RadTaskDialogPage With {
                            .Caption = " A Network Exception occurred",
                            .Heading = "A Network Exception occurred",
                            .Text = "ViVeTool-GUI is unable to populate the Build Combo Box, if the Device isn't connected to the Internet, or if the GitHub API is unreachable." & vbNewLine & vbNewLine & "You are still able to manually change a Feature ID by pressing F12, and able to load a local Feature List.",
                            .Icon = RadTaskDialogIcon.ShieldWarningYellowBar
                        }
                       RTD.CommandAreaButtons.Add(RadTaskDialogButton.Close)
                       RadTaskDialog.ShowDialog(RTD)
                   End Sub)
        End If
    End Sub

    ''' <summary>
    ''' Populates the Build Combo Box. Used at the Form_Load Event
    ''' </summary>
    Private Sub PopulateBuildComboBox()
        Dim RepoURL As String = "https://api.github.com/repos/riverar/mach2/git/trees/master?recursive=1"

        'Required Headers for the GitHub API.
        Dim WebClientFeatures As New WebClient With {
            .Encoding = System.Text.Encoding.UTF8
        }
        WebClientFeatures.Headers.Add(HttpRequestHeader.ContentType, "application/json; charset=utf-8")
        WebClientFeatures.Headers.Add(HttpRequestHeader.UserAgent, "PeterStrick/vivetool-gui")

        'Get every feature list under riverar/mach2/features, including the newer nested branch/architecture folders.
        Try
            Dim ContentsJSONFeatures As String = WebClientFeatures.DownloadString(RepoURL)
            Dim JSONObjectFeatures As JObject = JObject.Parse(ContentsJSONFeatures)
            Dim JSONArrayFeatures As JArray = CType(JSONObjectFeatures.SelectToken("tree"), JArray)

            Dim tempList As New List(Of String)

            For Each element In JSONArrayFeatures
                Dim featurePath As String = element("path").ToString

                If featurePath.StartsWith("features/", StringComparison.OrdinalIgnoreCase) AndAlso
                        featurePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) Then
                    tempList.Add(GetFeatureListDisplayName(featurePath))
                End If
            Next

            tempList.Sort(AddressOf CompareFeatureListNames)

            Invoke(Sub()
                       RDDL_Build.SortStyle = Telerik.WinControls.Enumerations.SortStyle.None

                       'Add the Items of tempList to the Combo Box
                       RDDL_Build.Items.AddRange(tempList)

                       'Deselect any Item
                       RDDL_Build.SelectedIndex = -1

                       'Set default Text
                       RDDL_Build.Text = "Select Build..."

                    End Sub)
            'Enable the Combo Box
            Invoke(Sub() RDDL_Build.Enabled = True)

            'Auto-load the newest Build if it is Enabled in the Settings
            If My.Settings.AutoLoad Then
                Invoke(Sub()
                           If RDDL_Build.Items.Count > 1 Then
                               RDDL_Build.SelectedIndex = 1
                           End If
                       End Sub)
            End If
        Catch webex As WebException
            Dim CopyExAndClose As New RadTaskDialogButton With {
                .Text = "Copy Exception and Close"
            }
            AddHandler CopyExAndClose.Click, New EventHandler(Sub()
                                                                  Try
                                                                      My.Computer.Clipboard.SetText(DirectCast(webex.Response, HttpWebResponse).StatusDescription)
                                                                  Catch clipex As Exception
                                                                      'Do nothing
                                                                  End Try
                                                              End Sub)

            Dim RTD As New RadTaskDialogPage With {
                    .Caption = " A Network Exception occurred",
                    .Heading = "A Network Exception occurred. Your IP may have been temporarily rate limited by the GitHub API for an hour.",
                    .Icon = RadTaskDialogIcon.ShieldErrorRedBar
                }
            Try
                RTD.Expander.Text = "GitHub API Response: " & DirectCast(webex.Response, HttpWebResponse).StatusDescription
            Catch ex As Exception
                RTD.Expander.Text = webex.ToString
            End Try
            RTD.Expander.ExpandedButtonText = "Collapse Exception"
            RTD.Expander.CollapsedButtonText = "Show Exception"
            RTD.CommandAreaButtons.Add(CopyExAndClose)
            RadTaskDialog.ShowDialog(RTD)
        Catch ex As Exception
            Dim CopyExAndClose As New RadTaskDialogButton With {
                .Text = "Copy Exception and Close"
            }
            AddHandler CopyExAndClose.Click, New EventHandler(Sub()
                                                                  Try
                                                                      My.Computer.Clipboard.SetText(ex.ToString)
                                                                  Catch clipex As Exception
                                                                      'Do nothing
                                                                  End Try
                                                              End Sub)

            Dim RTD As New RadTaskDialogPage With {
                    .Caption = " An Exception occurred",
                    .Heading = "An unknown Exception occurred.",
                    .Icon = RadTaskDialogIcon.ShieldErrorRedBar
                }
            RTD.Expander.Text = ex.ToString
            RTD.Expander.ExpandedButtonText = "Collapse Exception"
            RTD.Expander.CollapsedButtonText = "Show Exception"
            RTD.CommandAreaButtons.Add(CopyExAndClose)
            RadTaskDialog.ShowDialog(RTD)
        End Try
    End Sub

    Private Function GetFeatureListDisplayName(featurePath As String) As String
        Const FeaturePathPrefix As String = "features/"
        Const FeaturePathSuffix As String = ".txt"

        Dim displayName As String = featurePath

        If displayName.StartsWith(FeaturePathPrefix, StringComparison.OrdinalIgnoreCase) Then
            displayName = displayName.Substring(FeaturePathPrefix.Length)
        End If

        If displayName.EndsWith(FeaturePathSuffix, StringComparison.OrdinalIgnoreCase) Then
            displayName = displayName.Substring(0, displayName.Length - FeaturePathSuffix.Length)
        End If

        Return displayName
    End Function

    Private Function GetSelectedFeatureListPath() As String
        Dim selectedFeature As String = RDDL_Build.Text.Replace("\", "/")

        If selectedFeature.StartsWith("features/", StringComparison.OrdinalIgnoreCase) Then
            Return selectedFeature
        End If

        If selectedFeature.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) Then
            Return "features/" & selectedFeature
        End If

        Return "features/" & selectedFeature & ".txt"
    End Function

    Private Function GetFeatureListTempPath(featureListPath As String) As String
        Dim safeFileName As String = featureListPath.Replace("/", "_").Replace("\", "_").Replace(":", "_")
        Return IO.Path.Combine(IO.Path.GetTempPath(), safeFileName)
    End Function

    Private Function CompareFeatureListNames(left As String, right As String) As Integer
        Dim leftBuildNumber As Integer = ExtractBuildNumber(left)
        Dim rightBuildNumber As Integer = ExtractBuildNumber(right)
        Dim buildComparison As Integer = rightBuildNumber.CompareTo(leftBuildNumber)

        If buildComparison <> 0 Then
            Return buildComparison
        End If

        Return String.Compare(left, right, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function ExtractBuildNumber(featureListName As String) As Integer
        Dim fileName As String = IO.Path.GetFileName(featureListName.Replace("/", "\"))
        Dim buildNumberText As String = String.Empty

        For Each character As Char In fileName
            If Char.IsDigit(character) Then
                buildNumberText &= character
            ElseIf buildNumberText.Length > 0 Then
                Exit For
            End If
        Next

        Dim buildNumber As Integer = 0
        Integer.TryParse(buildNumberText, buildNumber)
        Return buildNumber
    End Function

    ''' <summary>
    ''' Override of OnHandleCreated(e As EventArgs).
    ''' Appends the About Element into the System Menu
    ''' </summary>
    ''' <param name="e">Default EventArgs</param>
    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)

        ' Get a handle to a copy of this form's system (window) menu
        Dim hSysMenu As IntPtr = GetSystemMenu(Me.Handle, False)

        ' Add a separator
        AppendMenu(hSysMenu, MF_SEPARATOR, 0, String.Empty)

        ' Add the Manually set Feature ID menu item
        AppendMenu(hSysMenu, MF_STRING, 2, "Manually Set Feature ID")

        ' Add a separator
        AppendMenu(hSysMenu, MF_SEPARATOR, 0, String.Empty)

        ' Add the About menu item
        AppendMenu(hSysMenu, MF_STRING, 1, "&About...")
    End Sub

    ''' <summary>
    ''' Overrides WndProc(ByRef m As Message).
    ''' Checks if the message ID and performs an action depending on the ID. Example: ID 1 Shows the About Dialog.
    ''' </summary>
    ''' <param name="m">Windows Forms Message to be sent.</param>
    Protected Overrides Sub WndProc(ByRef m As Message)
        MyBase.WndProc(m)

        ' Test if the About item was selected from the system menu
        If (m.Msg = WM_SYSCOMMAND) AndAlso (CInt(m.WParam) = 1) Then
            AboutAndSettings.ShowDialog()
        ElseIf (m.Msg = WM_SYSCOMMAND) AndAlso (CInt(m.WParam) = 2) Then
            SetManual.ShowDialog()
        End If
    End Sub

    ''' <summary>
    ''' Disables the Combo Box and runs the Background Worker each time the Combo Box Selected Index changes.
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub PopulateDataGridView(sender As Object, e As EventArgs) 'Handles RDDL_Build.SelectedIndexChanged
        'Disable Animations and selection. Helps with flickering
        Telerik.WinControls.AnimatedPropertySetting.AnimationsEnabled = False
        RGV_MainGridView.SelectionMode = GridViewSelectionMode.None

        'Close Combo Box. This needs to be done, because in some cases the Combo Box is half closed and half opened, allowing the user to change it, while the Background Worker is running, which will result in an exception.
        RDDL_Build.CloseDropDown()

        'Disable Combo Box
        RDDL_Build.Enabled = False

        'Remove the Search Row temporarily
        RGV_MainGridView.MasterView.TableSearchRow.Search("")
        RGV_MainGridView.MasterView.TableSearchRow.IsVisible = False

        'If "Load manually..." is selected, then load from a TXT File, else load normally
        If RDDL_Build.Text = "Load manually..." Then
            Dim TXTThread As New Threading.Thread(AddressOf LoadFromManualTXT) With {
                .IsBackground = True
            }
            TXTThread.SetApartmentState(Threading.ApartmentState.STA)
            TXTThread.Start()
        ElseIf RDDL_Build.Text = Nothing Then
            'Do Nothing
        Else
            'Run Background Worker
            BGW_PopulateGridView.RunWorkerAsync()
        End If
    End Sub

    ''' <summary>
    ''' Same code as BGW_PopulateGridView.RunWorkerAsync(), just that it get's the Feature List locally instead of from GitHub
    ''' </summary>
    Private Sub LoadFromManualTXT()
        'Make a new OpenFileDialog
        Dim OFD As New OpenFileDialog With {
                .InitialDirectory = "C:\",
                .Title = "Path to a Feature List",
                .Filter = "Feature List|*.txt"
            }

        If OFD.ShowDialog() = DialogResult.OK AndAlso IO.File.Exists(OFD.FileName) Then
            'Remove all Group Descriptors
            Invoke(Sub() RGV_MainGridView.GroupDescriptors.Clear())

            'Set Status Label
            Invoke(Sub() RLE_StatusLabel.Text = "Populating the Data Grid View... This can take a while.")
            'Clear Data Grid View
            Invoke(Sub() RGV_MainGridView.Rows.Clear())

            'For each line add a grid view entry
            Try
                For Each Line In IO.File.ReadAllLines(OFD.FileName)
                    If Line = "## Unknown:" Then
                        LineStage = "Modifiable"
                    ElseIf Line = "## Always Enabled:" Then
                        LineStage = "Always Enabled"
                    ElseIf Line = "## Enabled By Default:" Then
                        LineStage = "Enabled By Default"
                    ElseIf Line = "## Disabled By Default:" Then
                        LineStage = "Disabled by Default"
                    ElseIf Line = "## Always Disabled:" Then
                        LineStage = "Always Disabled"
                    End If
                    'Split the Line at the :
                    Dim Str As String() = Line.Split(CChar(":"))

                    'If the Line is not empty, continue
                    If Line IsNot "" AndAlso Line.Contains("#") = False Then
                        'Remove any Spaces from the first Str Array (Feature Name) and second Str Array (Feature ID)
                        Str(0).Replace(" ", "")
                        Str(1).Replace(" ", "")
                        'Get the Feature Enabled State from the currently processing line.
                        'FeatureManager.QueryFeatureConfiguration returns Nullable; HasValue=False means no override (= Default).
                        Dim cfg = FeatureManager.QueryFeatureConfiguration(CUInt(Str(1)), RTL_FEATURE_CONFIGURATION_TYPE.Runtime)
                        Dim State As String = If(cfg.HasValue, cfg.Value.EnabledState.ToString(), "Default")
                        Invoke(Sub() RGV_MainGridView.Rows.Add(Str(0), Str(1), State, LineStage))
                    End If
                Next
                'Move to the first row, remove the selection and change the Status Label to Done.
                Invoke(Sub()
                           RGV_MainGridView.CurrentRow = RGV_MainGridView.Rows.Item(0)
                           RGV_MainGridView.CurrentRow = Nothing
                           RLE_StatusLabel.Text = "Done."
                       End Sub)

                'Enable Grouping
                Dim LineGroup As New Telerik.WinControls.Data.GroupDescriptor()
                LineGroup.GroupNames.Add("FeatureInfo", ComponentModel.ListSortDirection.Ascending)
                Invoke(Sub() RGV_MainGridView.GroupDescriptors.Add(LineGroup))
            Catch ex As Exception
                Invoke(Sub()
                           'Catch Any Exception that may occur

                           'Create a Button that on Click, copies the Exception Text
                           Dim CopyExAndClose As New RadTaskDialogButton With {
                                .Text = "Copy Exception and Close"
                            }
                           AddHandler CopyExAndClose.Click, New EventHandler(Sub()
                                                                                 Try
                                                                                     My.Computer.Clipboard.SetText(ex.ToString)
                                                                                 Catch clipex As Exception
                                                                                     'Do nothing
                                                                                 End Try
                                                                             End Sub)

                           'Fancy Message Box
                           Dim RTD As New RadTaskDialogPage With {
                                    .Caption = " An Exception occurred",
                                    .Heading = "An unknown Exception occurred.",
                                    .Icon = RadTaskDialogIcon.ShieldErrorRedBar
                                }

                           'Add the Exception Text to the Expander
                           RTD.Expander.Text = ex.ToString

                           'Set the Text for the "Collapse Info" and "More Info" Buttons
                           RTD.Expander.ExpandedButtonText = "Collapse Exception"
                           RTD.Expander.CollapsedButtonText = "Show Exception"

                           'Add the Button to the Message Box
                           RTD.CommandAreaButtons.Add(CopyExAndClose)

                           'Show the Message Box
                           RadTaskDialog.ShowDialog(RTD)

                           'Clear the selection
                           RDDL_Build.SelectedIndex = -1
                           RDDL_Build.Enabled = True

                           'Make the Search Row Visible
                           RGV_MainGridView.MasterView.TableSearchRow.IsVisible = True
                       End Sub)
            End Try
        Else
            'Clear the selection
            Invoke(Sub()
                       RDDL_Build.SelectedIndex = -1
                       RDDL_Build.Enabled = True
                   End Sub)

            'Resume searching
            Invoke(Sub()
                       'Make the Search Row Visible
                       RGV_MainGridView.MasterView.TableSearchRow.IsVisible = True
                   End Sub)
        End If
    End Sub

    ''' <summary>
    ''' Background Worker that populates the Grid View with the following steps:
    ''' 1. Set Status Label and Clear Grid View Rows
    ''' 2. Prepare WebClient and Download a FeatureID Text File, corresponding to the selected Build to %TEMP%
    ''' 3. Fix the Line Formatting of the Text File and remove Comments
    ''' 4. For Each Line, add the Feature Name and Feature ID to the Grid View, while also determining the Feature EnabledState and adding that to the Grid View as well.
    ''' 5. At last, Move to the First Row, Clear the selection and change the Status Label to Done.
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub BGW_PopulateGridView_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles BGW_PopulateGridView.DoWork
        If Not BGW_PopulateGridView.CancellationPending Then
            Dim featureListPath As String = String.Empty
            Invoke(Sub() featureListPath = GetSelectedFeatureListPath())
            Dim buildNumber As Integer = ExtractBuildNumber(featureListPath)

            'Debug
            Diagnostics.Debug.WriteLine("Loading Build " & GetFeatureListDisplayName(featureListPath))

            'Remove all Group Descriptors
            Invoke(Sub() RGV_MainGridView.GroupDescriptors.Clear())

            'Set Status Label
            Invoke(Sub() RLE_StatusLabel.Text = "Populating the Data Grid View... This can take a while.")

            'Clear Data Grid View
            'Fix for a weird Bug that happens randomly while clearing the rows if the search row has text in it
            Try
                Invoke(Sub() RGV_MainGridView.Rows.Clear())
            Catch ex As Exception
                Diagnostics.Debug.WriteLine("Exception while clearing row. Build: " & GetFeatureListDisplayName(featureListPath) & ". " & ex.Message)
            End Try

            'Prepare Web Client and download Build TXT
            Dim WebClient As New WebClient With {
                    .Encoding = System.Text.Encoding.UTF8
                }
            Dim path As String = GetFeatureListTempPath(featureListPath)
            WebClient.DownloadFile("https://raw.githubusercontent.com/riverar/mach2/master/" & featureListPath, path)

            'For each line add a grid view entry
            For Each Line In IO.File.ReadAllLines(path)

                'Check Line Stage, used for Grouping
                Try
                    If buildNumber >= 17704 Then
                        If Line = "## Unknown:" Then
                            LineStage = "Modifiable"
                        ElseIf Line = "## Always Enabled:" Then
                            LineStage = "Always Enabled"
                        ElseIf Line = "## Enabled By Default:" Then
                            LineStage = "Enabled By Default"
                        ElseIf Line = "## Disabled By Default:" Then
                            LineStage = "Disabled by Default"
                        ElseIf Line = "## Always Disabled:" Then
                            LineStage = "Always Disabled"
                        End If
                    Else
                        LineStage = "Select Build 17704 or higher to use Grouping"
                    End If
                Catch ex As Exception
                    LineStage = "Error"
                End Try

                'Split the Line at the :
                Dim Str As String() = Line.Split(CChar(":"))

                'If the Line is not empty, continue
                If Line IsNot "" AndAlso Line.Contains("#") = False Then
                    'Remove any Spaces from the first Str Array (Feature Name) and second Str Array (Feature ID)
                    Str(0).Replace(" ", "")
                    Str(1).Replace(" ", "")

                    'Get the Feature Enabled State from the currently processing line.
                    'FeatureManager.QueryFeatureConfiguration returns Nullable; HasValue=False means no override (= Default).
                    Dim cfg = FeatureManager.QueryFeatureConfiguration(CUInt(Str(1)), RTL_FEATURE_CONFIGURATION_TYPE.Runtime)
                    Dim State As String = If(cfg.HasValue, cfg.Value.EnabledState.ToString(), "Default")
                    Invoke(Sub() RGV_MainGridView.Rows.Add(Str(0), Str(1), State, LineStage))
                End If
            Next

            'Move to the first row, remove the selection and change the Status Label to Done.
            Invoke(Sub()
                       RGV_MainGridView.CurrentRow = RGV_MainGridView.Rows.Item(0)
                       RGV_MainGridView.CurrentRow = Nothing
                       RLE_StatusLabel.Text = "Done."
                   End Sub)

            'Delete Feature List from %TEMP%
            IO.File.Delete(path)

            'Enable Grouping
            Dim LineGroup As New Telerik.WinControls.Data.GroupDescriptor()
            LineGroup.GroupNames.Add("FeatureInfo", ComponentModel.ListSortDirection.Ascending)
            Invoke(Sub() RGV_MainGridView.GroupDescriptors.Add(LineGroup))

            'Enable Animations and selection
            Invoke(Sub()
                       Telerik.WinControls.AnimatedPropertySetting.AnimationsEnabled = True
                       RGV_MainGridView.SelectionMode = GridViewSelectionMode.FullRowSelect
                   End Sub)

            'Make the Search Row Visible
            Invoke(Sub()
                       RGV_MainGridView.MasterView.TableSearchRow.IsVisible = True
                   End Sub)
        Else
            Return
        End If
    End Sub

    ''' <summary>
    ''' Upon Background Worker Completion, stop the Background Worker and re-enable the Combo Box
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub BGW_PopulateGridView_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles BGW_PopulateGridView.RunWorkerCompleted
        'End BGW
        BGW_PopulateGridView.CancelAsync()

        'Enable the Build Combo Box
        RDDL_Build.Enabled = True
    End Sub

    ''' <summary>
    ''' Enable Feature Button, enables the currently selected Feature.
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub RMI_ActivateF_Click(sender As Object, e As EventArgs) Handles RMI_ActivateF.Click
        'Stop Searching temporarily
        RGV_MainGridView.MasterView.TableSearchRow.SuspendSearch()

        'Set Selected Feature to Enabled
        SetConfig(RTL_FEATURE_ENABLED_STATE.Enabled)

        'Resume Searching
        RGV_MainGridView.MasterView.TableSearchRow.ResumeSearch()
    End Sub

    ''' <summary>
    ''' Disable Feature Button, disables the currently selected Feature.
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub RMI_DeactivateF_Click(sender As Object, e As EventArgs) Handles RMI_DeactivateF.Click
        'Stop Searching temporarily
        RGV_MainGridView.MasterView.TableSearchRow.SuspendSearch()

        'Set Selected Feature to Disabled
        SetConfig(RTL_FEATURE_ENABLED_STATE.Disabled)

        'Resume Searching
        RGV_MainGridView.MasterView.TableSearchRow.ResumeSearch()
    End Sub

    ''' <summary>
    ''' Revert Feature Button, reverts the currently selected Feature back to default values.
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub RMI_RevertF_Click(sender As Object, e As EventArgs) Handles RMI_RevertF.Click
        'Stop Searching temporarily
        RGV_MainGridView.MasterView.TableSearchRow.SuspendSearch()

        'Set Selected Feature to Default Values
        SetConfig(RTL_FEATURE_ENABLED_STATE.Default)

        'Resume Searching
        RGV_MainGridView.MasterView.TableSearchRow.ResumeSearch()
    End Sub

    ''' <summary>
    ''' Set's the Feature Configuration. Uses the state parameter to set the EnabledState of the Feature
    ''' </summary>
    ''' <param name="state">Specifies what Enabled State the Feature should be in. Can be either Enabled, Disabled or Default</param>
    Private Sub SetConfig(state As RTL_FEATURE_ENABLED_STATE)
        Try
            'Upstream API takes RTL_FEATURE_CONFIGURATION_UPDATE() array, not List(Of T)
            Dim _configs As RTL_FEATURE_CONFIGURATION_UPDATE() = {
                New RTL_FEATURE_CONFIGURATION_UPDATE() With {
                    .FeatureId = CUInt(RGV_MainGridView.SelectedRows.Item(0).Cells(1).Value),
                    .Priority = RTL_FEATURE_CONFIGURATION_PRIORITY.User,
                    .EnabledState = state,
                    .EnabledStateOptions = RTL_FEATURE_ENABLED_STATE_OPTIONS.None,
                    .Variant = 0UI,
                    .VariantPayload = 0UI,
                    .VariantPayloadKind = RTL_FEATURE_VARIANT_PAYLOAD_KIND.None,
                    .Operation = RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState
                }
            }

            'Write Boot first; on success mark LKG as BootPending so kernel picks it up on next boot.
            'Then write Runtime for live effect, preserving the old short-circuit behavior on Boot failures.
            Dim bootResult As Integer = FeatureManager.SetFeatureConfigurations(_configs, RTL_FEATURE_CONFIGURATION_TYPE.Boot)
            Dim bootPendingResult As Integer = 0
            Dim liveResult As Integer = 0
            Dim bootDetails As String = String.Empty

            If bootResult = 0 Then
                bootResult = EnsureBootConfigurationsStored(_configs, bootDetails)

                If bootResult = 0 AndAlso TrySetBootPending(bootPendingResult) Then
                    liveResult = FeatureManager.SetFeatureConfigurations(_configs, RTL_FEATURE_CONFIGURATION_TYPE.Runtime)
                End If
            End If

            If bootResult <> 0 OrElse bootPendingResult <> 0 OrElse liveResult <> 0 Then
                Dim errorDetails As String = GetSetConfigErrorDetails(bootResult, bootPendingResult, liveResult)
                If Not String.IsNullOrWhiteSpace(bootDetails) Then
                    errorDetails &= vbNewLine & vbNewLine & bootDetails
                End If

                'Set Status Label
                RLE_StatusLabel.Text = "An error occurred while setting a feature configuration for " & RGV_MainGridView.SelectedRows.Item(0).Cells(0).Value.ToString

                'Fancy Message Box
                Dim RTD As New RadTaskDialogPage With {
                    .Caption = " An Error occurred",
                    .Heading = "An Error occurred while trying to set Feature " & RGV_MainGridView.SelectedRows.Item(0).Cells(0).Value.ToString & " to " & state.ToString(),
                    .Icon = RadTaskDialogIcon.Error
                }

                RTD.Expander.Text = errorDetails
                RTD.Expander.ExpandedButtonText = "Collapse Details"
                RTD.Expander.CollapsedButtonText = "Show Details"

                'Add a Close Button instead of a OK Button
                RTD.CommandAreaButtons.Add(RadTaskDialogButton.Close)

                'Show the Message Box
                RadTaskDialog.ShowDialog(RTD)
            Else
                'Set Status Label
                RLE_StatusLabel.Text = "Successfully set feature configuration for" & RGV_MainGridView.SelectedRows.Item(0).Cells(0).Value.ToString & " with Value " & state.ToString()

                'Set Cell Text
                RGV_MainGridView.CurrentRow.Cells.Item(2).Value = state.ToString()

                'Fancy Message Box
                Dim RTD As New RadTaskDialogPage With {
                    .Caption = " Success",
                    .Heading = "Successfully set Feature " & RGV_MainGridView.SelectedRows.Item(0).Cells(0).Value.ToString & " to " & state.ToString(),
                    .Icon = RadTaskDialogIcon.ShieldSuccessGreenBar
                }

                If Not String.IsNullOrWhiteSpace(bootDetails) Then
                    RTD.Expander.Text = bootDetails
                    RTD.Expander.ExpandedButtonText = "Collapse Details"
                    RTD.Expander.CollapsedButtonText = "Show Details"
                End If

                'Add a Close Button instead of a OK Button
                RTD.CommandAreaButtons.Add(RadTaskDialogButton.Close)

                'Show the Message Box
                RadTaskDialog.ShowDialog(RTD)
            End If
        Catch ex As Exception
            'Catch Any Exception that may occur

            'Create a Button that on Click, copies the Exception Text
            Dim CopyExAndClose As New RadTaskDialogButton With {
                .Text = "Copy Exception and Close"
            }
            AddHandler CopyExAndClose.Click, New EventHandler(Sub()
                                                                  Try
                                                                      My.Computer.Clipboard.SetText(ex.ToString)
                                                                  Catch clipex As Exception
                                                                      'Do nothing
                                                                  End Try
                                                              End Sub)

            'Fancy Message Box
            Dim RTD As New RadTaskDialogPage With {
                    .Caption = " An Exception occurred",
                    .Heading = "An unknown Exception occurred.",
                    .Icon = RadTaskDialogIcon.ShieldErrorRedBar
                }

            'Add the Exception Text to the Expander
            RTD.Expander.Text = ex.ToString

            'Set the Text for the "Collapse Info" and "More Info" Buttons
            RTD.Expander.ExpandedButtonText = "Collapse Exception"
            RTD.Expander.CollapsedButtonText = "Show Exception"

            'Add the Button to the Message Box
            RTD.CommandAreaButtons.Add(CopyExAndClose)

            'Show the Message Box
            RadTaskDialog.ShowDialog(RTD)
        End Try
    End Sub

    Private Function EnsureBootConfigurationsStored(configs As RTL_FEATURE_CONFIGURATION_UPDATE(), ByRef details As String) As Integer
        Dim detailLines As New List(Of String)

        For Each config In configs
            Dim configDetails As String = String.Empty
            Dim result As Integer = EnsureBootConfigurationStored(config, configDetails)
            If Not String.IsNullOrWhiteSpace(configDetails) Then
                detailLines.Add(configDetails)
            End If

            If result <> 0 Then
                details = String.Join(vbNewLine, detailLines)
                Return result
            End If
        Next

        details = String.Join(vbNewLine, detailLines)
        Return 0
    End Function

    Private Function EnsureBootConfigurationStored(config As RTL_FEATURE_CONFIGURATION_UPDATE, ByRef details As String) As Integer
        If config.Priority = RTL_FEATURE_CONFIGURATION_PRIORITY.UserPolicy Then
            details = "Boot registry verification skipped for UserPolicy priority."
            Return 0
        End If

        Dim overrideKeyPath As String = GetBootOverrideKeyPath(config)
        Dim displayPath As String = "HKLM:\" & overrideKeyPath

        If BootConfigurationMatches(config, overrideKeyPath) Then
            details = "Boot registry verified at " & displayPath & " (" & GetBootConfigurationDetails(config) & ")."
            Return 0
        End If

        Try
            Using baseKey = OpenLocalMachineBaseKey()
                If config.Operation = RTL_FEATURE_CONFIGURATION_OPERATION.ResetState Then
                    baseKey.DeleteSubKeyTree(overrideKeyPath, False)
                    baseKey.Flush()
                    details = "Boot registry reset verified at " & displayPath & "."
                    Return 0
                End If

                Using rKey = baseKey.CreateSubKey(overrideKeyPath)
                    If rKey Is Nothing Then
                        details = "Boot registry write failed: CreateSubKey returned Nothing for " & displayPath & "."
                        Return -1073741823 '0xC0000001
                    End If

                    If config.Operation.HasFlag(RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState) Then
                        rKey.SetValue("EnabledState", CInt(config.EnabledState), Microsoft.Win32.RegistryValueKind.DWord)
                        rKey.SetValue("EnabledStateOptions", CInt(config.EnabledStateOptions), Microsoft.Win32.RegistryValueKind.DWord)
                    End If

                    If config.Operation.HasFlag(RTL_FEATURE_CONFIGURATION_OPERATION.VariantState) Then
                        rKey.SetValue("Variant", CInt(config.Variant), Microsoft.Win32.RegistryValueKind.DWord)
                        rKey.SetValue("VariantPayload", CInt(config.VariantPayload), Microsoft.Win32.RegistryValueKind.DWord)
                        rKey.SetValue("VariantPayloadKind", CInt(config.VariantPayloadKind), Microsoft.Win32.RegistryValueKind.DWord)
                    End If

                    rKey.Flush()
                End Using

                baseKey.Flush()
            End Using

            If BootConfigurationMatches(config, overrideKeyPath) Then
                details = "Boot registry was missing; wrote and verified " & displayPath & " (" & GetBootConfigurationDetails(config) & ")."
                Return 0
            End If

            details = "Boot registry write verification failed at " & displayPath & "."
            Return -1073741823 '0xC0000001
        Catch ex As Exception
            details = "Boot registry write threw " & ex.GetType().Name & " at " & displayPath & ": " & ex.Message
            Return ex.HResult
        End Try
    End Function

    Private Function BootConfigurationMatches(config As RTL_FEATURE_CONFIGURATION_UPDATE, overrideKeyPath As String) As Boolean
        Try
            Using baseKey = OpenLocalMachineBaseKey()
                Using rKey = baseKey.OpenSubKey(overrideKeyPath)
                    If rKey Is Nothing Then
                        Return config.Operation = RTL_FEATURE_CONFIGURATION_OPERATION.ResetState
                    End If

                    If config.Operation = RTL_FEATURE_CONFIGURATION_OPERATION.ResetState Then
                        Return False
                    End If

                    If config.Operation.HasFlag(RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState) Then
                        If Convert.ToInt32(rKey.GetValue("EnabledState", -1)) <> CInt(config.EnabledState) Then
                            Return False
                        End If

                        If Convert.ToInt32(rKey.GetValue("EnabledStateOptions", -1)) <> CInt(config.EnabledStateOptions) Then
                            Return False
                        End If
                    End If

                    If config.Operation.HasFlag(RTL_FEATURE_CONFIGURATION_OPERATION.VariantState) Then
                        If Convert.ToInt32(rKey.GetValue("Variant", -1)) <> CInt(config.Variant) Then
                            Return False
                        End If

                        If Convert.ToInt32(rKey.GetValue("VariantPayload", -1)) <> CInt(config.VariantPayload) Then
                            Return False
                        End If

                        If Convert.ToInt32(rKey.GetValue("VariantPayloadKind", -1)) <> CInt(config.VariantPayloadKind) Then
                            Return False
                        End If
                    End If

                    Return True
                End Using
            End Using
        Catch
            Return False
        End Try
    End Function

    Private Function GetBootOverrideKeyPath(config As RTL_FEATURE_CONFIGURATION_UPDATE) As String
        Dim obfuscatedId As String = ObfuscationHelpers.ObfuscateFeatureId(config.FeatureId).ToString()
        Return "SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\" & CInt(config.Priority).ToString() & "\" & obfuscatedId
    End Function

    Private Function GetBootConfigurationDetails(config As RTL_FEATURE_CONFIGURATION_UPDATE) As String
        Return "FeatureId=" & config.FeatureId.ToString() &
            ", Priority=" & CInt(config.Priority).ToString() &
            ", EnabledState=" & CInt(config.EnabledState).ToString() &
            ", EnabledStateOptions=" & CInt(config.EnabledStateOptions).ToString()
    End Function

    Private Function OpenLocalMachineBaseKey() As Microsoft.Win32.RegistryKey
        Return Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64)
    End Function

    Private Function TrySetBootPending(ByRef result As Integer) As Boolean
        Const StatusObjectNameNotFound As Integer = -1073741772 '0xC0000034

        Dim currentState As BSD_FEATURE_CONFIGURATION_STATE = BSD_FEATURE_CONFIGURATION_STATE.Uninitialized
        result = FeatureManager.GetBootFeatureConfigurationState(currentState)

        If result <> 0 Then
            If result <> StatusObjectNameNotFound Then
                Return False
            End If

            result = FeatureManager.InitializeBootStatusDataFile()
            If result <> 0 Then
                Return False
            End If

            currentState = BSD_FEATURE_CONFIGURATION_STATE.Uninitialized
        End If

        If currentState <> BSD_FEATURE_CONFIGURATION_STATE.BootPending Then
            result = FeatureManager.SetBootFeatureConfigurationState(BSD_FEATURE_CONFIGURATION_STATE.BootPending)
            If result <> 0 Then
                Return False
            End If
        End If

        result = 0
        Return True
    End Function

    Private Function GetSetConfigErrorDetails(bootResult As Integer, bootPendingResult As Integer, liveResult As Integer) As String
        If bootResult <> 0 Then
            Return "Boot store failed with " & FormatStatusCode(bootResult) & ". Runtime store was not changed."
        End If

        If bootPendingResult <> 0 Then
            Return "Boot status update failed with " & FormatStatusCode(bootPendingResult) & ". Runtime store was not changed."
        End If

        If liveResult <> 0 Then
            Return "Runtime store failed with " & FormatStatusCode(liveResult) & ". Boot store was changed."
        End If

        Return "Feature configuration failed for an unknown reason."
    End Function

    Private Function FormatStatusCode(result As Integer) As String
        Return "0x" & (CLng(result) And 4294967295L).ToString("X8")
    End Function

    ''' <summary>
    ''' Selection Changed Event. Used to enable the RDDB_PerformAction Button, upon selecting a row.
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub RGV_MainGridView_SelectionChanged(sender As Object, e As EventArgs) Handles RGV_MainGridView.SelectionChanged
        If RGV_MainGridView.CurrentRow Is Nothing Then
            RDDB_PerformAction.Enabled = False
        Else
            RDDB_PerformAction.Enabled = True
        End If
    End Sub

    ''' <summary>
    ''' Click Event. Used to show the About Box
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub RB_About_Click(sender As Object, e As EventArgs) Handles RB_About.Click
        AboutAndSettings.ShowDialog()
    End Sub

    ''' <summary>
    ''' Show the Manually Set Feature ID UI when F12 is pressed
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Key EventArgs</param>
    Private Sub GUI_KeyDown(sender As Object, e As KeyEventArgs) Handles MyBase.KeyDown
        If e.KeyCode = Keys.F12 Then
            SetManual.ShowDialog()
        End If
    End Sub

    ''' <summary>
    ''' Shows the UI to manually set a Feature ID
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub RB_ManuallySetFeature_Click(sender As Object, e As EventArgs) Handles RB_ManuallySetFeature.Click
        SetManual.ShowDialog()
    End Sub

    ''' <summary>
    ''' Basic Internet Connectivity Check by trying to check if github.com is accessible
    ''' </summary>
    ''' <returns>True if http://www.github.com responds. False if not</returns>
    Public Shared Function CheckForInternetConnection() As Boolean
        Try
            Using client = New WebClient()
                Using stream = client.OpenRead("http://www.github.com")
                    Return True
                End Using
            End Using
        Catch
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Form Closing Event. Used to forcefully close ViVeTool GUI and it's Threads
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">FormClosing EventArgs</param>
    Private Sub GUI_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        'Exit all running Threads forcefully, should fix ObjectDisposed Exceptions
        Diagnostics.Process.GetCurrentProcess().Kill()
    End Sub
End Class
