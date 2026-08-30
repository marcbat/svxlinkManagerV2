namespace SvxlinkManagerV2.Application.Features.Diagnostics;

/// <summary>
/// Archive de diagnostic prête à être téléchargée.
/// </summary>
/// <param name="FileName">Nom de fichier proposé au navigateur, horodaté.</param>
/// <param name="Content">Contenu de l'archive ZIP.</param>
public record DiagnosticArchiveDto(string FileName, byte[] Content);
