using Microsoft.Extensions.Options;

namespace SignalCli.Models;

/// <summary>
/// D.9: compile-time-генерований <see cref="IValidateOptions{TOptions}"/>-валідатор
/// для <see cref="SignalCliOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// Атрибут <see cref="OptionsValidatorAttribute"/> вмикає source-generator із пакета
/// <c>Microsoft.Extensions.Options</c> (включений починаючи з .NET 8). Генератор
/// створює реалізацію <see cref="IValidateOptions{TOptions}.Validate"/>, яка перевіряє
/// усі DataAnnotations (<c>[Required]</c>, <c>[Range]</c>, …) <i>без reflection</i> —
/// це швидше, виділяє менше пам’яті, і працює під Native AOT/trim.
/// </para>
/// <para>
/// Зареєстровано у <c>ServiceCollectionExtensions.AddSignalCli</c> через
/// <c>services.AddSingleton&lt;IValidateOptions&lt;SignalCliOptions&gt;, SignalCliOptionsValidator&gt;()</c>;
/// викликається автоматично при першому доступі до <see cref="IOptions{TOptions}.Value"/>.
/// </para>
/// </remarks>
[OptionsValidator]
internal sealed partial class SignalCliOptionsValidator : IValidateOptions<SignalCliOptions>
{
}
