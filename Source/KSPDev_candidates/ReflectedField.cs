// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System.Reflection;
using KSPDev.LogUtils;

namespace KSPDev.ProcessingUtils {

/// <summary>Wrapper to implement efficient access to the class fields via reflection.</summary>
/// <remarks>It ignores access scope.</remarks>
/// <typeparam name="T">type of the class.</typeparam>
/// <typeparam name="V">type of the field value.</typeparam>
public class ReflectedField<T, V> {
  readonly FieldInfo _fieldInfo;

  public ReflectedField(string fieldName) {
    _fieldInfo = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    if (_fieldInfo == null) {
      DebugEx.Error("Cannot obtain field {0} from {1}", fieldName, typeof(T));
    }
  }

  /// <summary>Indicates if the target field was found and ready to use.</summary>
  public bool IsValid() {
    return _fieldInfo != null;
  }

  /// <summary>Gets the field value or returns a default value if the field is not found.</summary>
  public V Get(T instance) {
    return _fieldInfo != null ? (V)_fieldInfo.GetValue(instance) : default(V);
  }

  /// <summary>Gets the field value or returns the provided default value if the field is not found.</summary>
  public V Get(T instance, V defaultValue) {
    return _fieldInfo != null ? (V)_fieldInfo.GetValue(instance) : defaultValue;
  }

  /// <summary>Sets the field value or does nothing if the field is not found.</summary>
  public void Set(T instance, V value) {
    if (_fieldInfo != null) {
      _fieldInfo.SetValue(instance, value);
    }
  }
}

}
