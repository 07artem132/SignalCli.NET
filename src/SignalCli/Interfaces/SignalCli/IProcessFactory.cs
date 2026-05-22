using System.Diagnostics;

namespace SignalCli.Interfaces.SignalCli;

/// <summary>
/// Фабрика для створення об'єктів IProcess.
/// </summary>
public interface IProcessFactory
{
    /// <summary>
    /// Створює об'єкт IProcess з вказаними параметрами запуску.
    /// </summary>
    /// <param name="startInfo">Параметри запуску процесу.</param>
    /// <returns>Новий екземпляр IProcess.</returns>
    IProcess CreateProcess(ProcessStartInfo startInfo);
}