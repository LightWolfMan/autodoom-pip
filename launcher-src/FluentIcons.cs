using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;

namespace AutoDoomLauncher;

/// <summary>
/// Os icones da janela. Sao os Fluent UI System Icons da Microsoft (MIT), os
/// mesmos do Windows 11, rasterizados uma vez em 256x256 preto sobre fundo
/// transparente e embutidos no executavel em `Icons/`.
///
/// Por que preto e nao a cor final: a cor sai do tema, que muda em tempo de
/// execucao. Aqui o desenho e so a mascara -- o alfa do PNG -- e a cor entra na
/// hora de usar, por uma ColorMatrix que zera o RGB e mantem o alfa. Um arquivo
/// serve para os dois temas.
///
/// Por que 256 e nao o tamanho de uso: o launcher precisa dos icones em 16, 24,
/// 28, 38 e 100 pixels, e ainda multiplicado pela escala do monitor. Reduzir de
/// 256 com bicubica de qualidade da um icone limpo em qualquer um desses; o
/// caminho contrario, ampliar, borraria.
/// </summary>
internal static class FluentIcons
{
   /// <summary>Chave do cache: o mesmo icone em outro tamanho ou cor e outro bitmap.</summary>
   private static readonly Dictionary<(string Name, int Size, int Argb), Bitmap> Cache = [];

   /// <summary>Mascaras 256x256 ja lidas do recurso, guardadas para nao reler.</summary>
   private static readonly Dictionary<string, Bitmap?> Masks = new(StringComparer.OrdinalIgnoreCase);

   /// <summary>
   /// Icone `name` (o nome do arquivo em `Icons/`, sem extensao) com `size`
   /// pixels de lado, pintado de `color`. Devolve null se o recurso sumir --
   /// icone faltando nao pode derrubar a janela.
   /// </summary>
   public static Bitmap? Get(string name, int size, Color color)
   {
      var key = (name, size, color.ToArgb());
      if (Cache.TryGetValue(key, out Bitmap? cached))
         return cached;

      Bitmap? mask = LoadMask(name);
      if (mask is null)
         return null;

      var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
      using (Graphics g = Graphics.FromImage(bmp))
      {
         g.InterpolationMode = InterpolationMode.HighQualityBicubic;
         g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
         g.CompositingQuality = CompositingQuality.HighQuality;

         // Zera o RGB de origem e soma a cor pedida na linha de translacao; a
         // terceira coluna da diagonal preserva o alfa, que e o desenho.
         var matrix = new ColorMatrix(
         [
            [0f, 0f, 0f, 0f, 0f],
            [0f, 0f, 0f, 0f, 0f],
            [0f, 0f, 0f, 0f, 0f],
            [0f, 0f, 0f, 1f, 0f],
            [color.R / 255f, color.G / 255f, color.B / 255f, 0f, 1f],
         ]);

         using var attributes = new ImageAttributes();
         attributes.SetColorMatrix(matrix);
         g.DrawImage(mask, new Rectangle(0, 0, size, size),
                     0, 0, mask.Width, mask.Height, GraphicsUnit.Pixel, attributes);
      }

      Cache[key] = bmp;
      return bmp;
   }

   private static Bitmap? LoadMask(string name)
   {
      if (Masks.TryGetValue(name, out Bitmap? cached))
         return cached;

      Bitmap? mask = null;
      try
      {
         Assembly assembly = typeof(FluentIcons).Assembly;
         using Stream? stream = assembly.GetManifestResourceStream($"AutoDoomLauncher.Icons.{name}.png");
         if (stream is not null)
            mask = new Bitmap(stream);
      }
      catch (Exception e) when (e is IOException or ArgumentException)
      {
         mask = null;
      }

      Masks[name] = mask;
      return mask;
   }
}
