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
Imports Albacore.ViVe
Imports Albacore.ViVe.NativeEnums
Imports Albacore.ViVe.NativeStructs
Imports Telerik.WinControls.UI

''' <summary>
''' ViVeTool GUI - Manual Feature ID UI
''' </summary>
Public Class SetManual
    ''' <summary>
    ''' Set's the Feature Configuration. Uses the state parameter to set the EnabledState of the Feature
    ''' </summary>
    ''' <param name="state">Specifies what Enabled State the Feature should be in. Can be either Enabled, Disabled or Default</param>
    Private Sub SetConfig_Manual(state As RTL_FEATURE_ENABLED_STATE)
        Try
            'Upstream API takes RTL_FEATURE_CONFIGURATION_UPDATE() array, not List(Of T)
            Dim _configs As RTL_FEATURE_CONFIGURATION_UPDATE() = {
                New RTL_FEATURE_CONFIGURATION_UPDATE() With {
                    .FeatureId = CUInt(RTB_FeatureID.Text),
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

                Dim RTD As New RadTaskDialogPage With {
                    .Caption = " An Error occurred",
                    .Heading = "An Error occurred while trying to set Feature ID " & RTB_FeatureID.Text & " to " & state.ToString(),
                    .Icon = RadTaskDialogIcon.Error
                }
                RTD.Expander.Text = errorDetails
                RTD.Expander.ExpandedButtonText = "Collapse Details"
                RTD.Expander.CollapsedButtonText = "Show Details"
                RTD.CommandAreaButtons.Add(RadTaskDialogButton.Close)
                RadTaskDialog.ShowDialog(RTD)
            Else
                Dim RTD As New RadTaskDialogPage With {
                    .Caption = " Success",
                    .Heading = "Successfully set Feature ID " & RTB_FeatureID.Text & " to " & state.ToString(),
                    .Icon = RadTaskDialogIcon.ShieldSuccessGreenBar
                }
                If Not String.IsNullOrWhiteSpace(bootDetails) Then
                    RTD.Expander.Text = bootDetails
                    RTD.Expander.ExpandedButtonText = "Collapse Details"
                    RTD.Expander.CollapsedButtonText = "Show Details"
                End If
                RTD.CommandAreaButtons.Add(RadTaskDialogButton.Close)
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
                    .Heading = "An Exception occurred while trying to set Feature ID " & RTB_FeatureID.Text & " to " & state.ToString(),
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
    ''' Enable Feature Button, enables the currently selected Feature.
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub RMI_ActivateF_Click(sender As Object, e As EventArgs) Handles RMI_ActivateF.Click
        RTB_FeatureID.Text = RTB_FeatureID.Text.Trim()
        SetConfig_Manual(RTL_FEATURE_ENABLED_STATE.Enabled)
    End Sub

    ''' <summary>
    ''' Disable Feature Button, disables the currently selected Feature.
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub RMI_DeactivateF_Click(sender As Object, e As EventArgs) Handles RMI_DeactivateF.Click
        RTB_FeatureID.Text = RTB_FeatureID.Text.Trim()
        SetConfig_Manual(RTL_FEATURE_ENABLED_STATE.Disabled)
    End Sub

    ''' <summary>
    ''' Revert Feature Button, reverts the currently selected Feature back to default settings.
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub RMI_RevertF_Click(sender As Object, e As EventArgs) Handles RMI_RevertF.Click
        RTB_FeatureID.Text = RTB_FeatureID.Text.Trim()
        SetConfig_Manual(RTL_FEATURE_ENABLED_STATE.Default)
    End Sub

    ''' <summary>
    ''' Used to help RTB_FeatureID_KeyPress by checking if the last Character in the Text Box is a Number. If yes the Drop Down Button get's enabled.
    ''' </summary>
    ''' <param name="sender">Default sender Object</param>
    ''' <param name="e">Default EventArgs</param>
    Private Sub RTB_FeatureID_TextChanged(sender As Object, e As EventArgs) Handles RTB_FeatureID.TextChanged
        If IsNumeric(RTB_FeatureID.Text) Then
            RDDB_PerformAction.Enabled = True
        Else
            RDDB_PerformAction.Enabled = False
        End If
    End Sub
End Class
