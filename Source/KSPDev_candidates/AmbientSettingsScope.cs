// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using KSPDev.LogUtils;
using UnityEngine;
using UnityEngine.Rendering;

namespace KSPDev.ModelUtils {

/// <summary>A utility class to run local renders with the changed ambient light settings.</summary>
/// <remarks>
/// <p>
/// When the scope starts, it saves all the ambient related settings in Unity. On the scope end, the original values are
/// restored.
/// </p>
/// <p>
/// This scope saves the following values:
/// <ul>
/// <li>All properties of <c>RenderSettings</c> that start from prefix "ambient".</li>
/// <li><see cref="AmbientBoostDiffuse"/> and <see cref="AmbientBoostEmissive"/> global properties of the shader.</li> 
/// </ul> 
/// </p>
/// </remarks>
/// <example>
/// <code><![CDATA[
/// // When the context starts, the ambient settings are saved.
/// using (new AmbientSettingsScope() {
///   RenderSettings.ambientIntensity = 0.5f;
///   Shader.SetGlobalFloat(AmbientSettingsScope.AmbientBoostDiffuse, 0f);
///   Shader.SetGlobalFloat(AmbientSettingsScope.AmbientBoostEmissive, 10f);
///   // ...call the rendering code...
/// }
/// // On exit the original settings will be restored.
/// ]]></code>
/// </example>
/// <seealso href="https://docs.unity3d.com/ScriptReference/RenderSettings.html">RenderSettings</seealso>
public class AmbientSettingsScope : IDisposable {
	#region API fields and properties
	/// <summary>Global shader diffuse ambient boost property.</summary>
	public static readonly int AmbientBoostDiffuse = Shader.PropertyToID("_SpecularAmbientBoostDiffuse");

	/// <summary>Global shader emissive ambient boost property.</summary>
	public static readonly int AmbientBoostEmissive = Shader.PropertyToID("_SpecularAmbientBoostEmissive");
	#endregion

	#region Local fields and properties
	readonly Color _ambientLight;
	readonly Color _ambientEquatorColor;
	readonly Color _ambientGroundColor;
	readonly Color _ambientSkyColor;
	readonly float _ambientIntensity;
	readonly AmbientMode _ambientMode;
	readonly float _shaderDiffuseBoost;
	readonly float _shaderEmissiveBoost;
	#endregion

	public AmbientSettingsScope(bool logCapturedValues = false) {
		_ambientLight = RenderSettings.ambientLight;
		_ambientEquatorColor = RenderSettings.ambientEquatorColor;
		_ambientGroundColor = RenderSettings.ambientGroundColor;
		_ambientSkyColor = RenderSettings.ambientSkyColor;
		_ambientIntensity = RenderSettings.ambientIntensity;
		_ambientMode = RenderSettings.ambientMode;
		_shaderDiffuseBoost = Shader.GetGlobalFloat(AmbientBoostDiffuse);
		_shaderEmissiveBoost = Shader.GetGlobalFloat(AmbientBoostEmissive);
		if (logCapturedValues) {
			DebugEx.Info(
					"Capture RenderSettings: ambientLight={0}, ambientEquatorColor={1}, ambientGroundColor={2},"
					+ " ambientSkyColor={3}, ambientIntensity={4}, ambientMode={5},"
					+ " shaderDiffuseBoost={6}, shaderEmissiveBoost={7}",
					_ambientLight, _ambientEquatorColor, _ambientGroundColor, _ambientSkyColor, _ambientIntensity, _ambientMode,
					_shaderDiffuseBoost, _shaderEmissiveBoost);
		}
	}
	public void Dispose() {
		RenderSettings.ambientLight = _ambientLight;
		RenderSettings.ambientEquatorColor = _ambientEquatorColor;
		RenderSettings.ambientGroundColor = _ambientGroundColor;
		RenderSettings.ambientSkyColor = _ambientSkyColor;
		RenderSettings.ambientIntensity = _ambientIntensity;
		RenderSettings.ambientMode = _ambientMode;
		Shader.SetGlobalFloat(AmbientBoostDiffuse, _shaderDiffuseBoost);
		Shader.SetGlobalFloat(AmbientBoostEmissive, _shaderEmissiveBoost);
	}
}

}  // namespace
