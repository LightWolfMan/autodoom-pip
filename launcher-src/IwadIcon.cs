using System.Drawing.Imaging;
using System.Text;

namespace AutoDoomLauncher;

/// <summary>
/// Tira o logo de dentro do proprio IWAD escolhido pelo usuario, em vez de
/// baixar arte de algum lugar: o arquivo ja esta na maquina dele, cada jogo tem
/// o seu, e nada de terceiro entra no repositorio.
///
/// Le o lump `M_DOOM` (o logo do menu) e, se nao houver, `TITLEPIC`. Os dois
/// estao no formato de patch do Doom, que e colunar: um cabecalho com largura,
/// altura e deslocamentos, e depois "posts" verticais de pixels indexados na
/// paleta `PLAYPAL`.
/// </summary>
internal static class IwadIcon
{
   private const int MaxWidth  = 512;
   private const int MaxHeight = 256;

   private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);

   /// <summary>Logo do IWAD, reduzido para caber em `size` pixels de altura.</summary>
   public static Bitmap? Load(string wadPath, int size)
   {
      string key = wadPath + "|" + size;
      if (Cache.TryGetValue(key, out Bitmap? cached))
         return cached;

      Bitmap? icon = null;
      try
      {
         icon = Extract(wadPath, size);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException or EndOfStreamException
                                     or InvalidDataException or ArgumentException)
      {
         icon = null;   // IWAD estranho nao pode derrubar a lista
      }

      Cache[key] = icon;
      return icon;
   }

   private static Bitmap? Extract(string wadPath, int size)
   {
      using FileStream file = File.OpenRead(wadPath);
      using var reader = new BinaryReader(file, Encoding.ASCII);

      string magic = new(reader.ReadChars(4));
      if (magic is not ("IWAD" or "PWAD"))
         return null;

      int count  = reader.ReadInt32();
      int offset = reader.ReadInt32();
      if (count <= 0 || count > 65536 || offset < 12 || offset >= file.Length)
         return null;

      var lumps = new Dictionary<string, (int Pos, int Size)>(StringComparer.OrdinalIgnoreCase);
      file.Position = offset;

      for (int i = 0; i < count; i++)
      {
         int pos  = reader.ReadInt32();
         int len  = reader.ReadInt32();
         string name = new string(reader.ReadChars(8)).TrimEnd('\0', ' ');
         lumps.TryAdd(name, (pos, len));
      }

      if (!lumps.TryGetValue("PLAYPAL", out (int Pos, int Size) pal) || pal.Size < 768)
         return null;

      file.Position = pal.Pos;
      byte[] palette = reader.ReadBytes(768);

      // M_DOOM e o logo do menu: pequeno, com fundo transparente, perfeito para
      // uma lista. TITLEPIC e o plano B, e vem inteiro.
      foreach (string candidate in new[] { "M_DOOM", "TITLE", "TITLEPIC", "INTERPIC" })
      {
         if (!lumps.TryGetValue(candidate, out (int Pos, int Size) lump) || lump.Size < 12)
            continue;

         Bitmap? patch = ReadPatch(file, reader, lump, palette);
         if (patch is not null)
            return Fit(patch, size);
      }

      return null;
   }

   private static Bitmap? ReadPatch(FileStream file, BinaryReader reader,
                                    (int Pos, int Size) lump, byte[] palette)
   {
      file.Position = lump.Pos;

      int width  = reader.ReadInt16();
      int height = reader.ReadInt16();
      reader.ReadInt16();   // deslocamento horizontal, irrelevante aqui
      reader.ReadInt16();   // deslocamento vertical

      if (width is <= 0 or > MaxWidth || height is <= 0 or > MaxHeight)
         return null;

      int[] columns = new int[width];
      for (int i = 0; i < width; i++)
         columns[i] = reader.ReadInt32();

      var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

      for (int x = 0; x < width; x++)
      {
         int columnStart = lump.Pos + columns[x];
         if (columnStart < 0 || columnStart >= file.Length)
            continue;

         file.Position = columnStart;

         // Cada coluna e uma pilha de posts; 255 fecha a coluna.
         while (true)
         {
            int top = reader.ReadByte();
            if (top == 0xFF)
               break;

            int length = reader.ReadByte();
            reader.ReadByte();   // byte de enchimento antes dos pixels

            for (int i = 0; i < length; i++)
            {
               byte index = reader.ReadByte();
               int y = top + i;
               if (y >= 0 && y < height)
               {
                  bitmap.SetPixel(x, y, Color.FromArgb(255,
                     palette[index * 3], palette[index * 3 + 1], palette[index * 3 + 2]));
               }
            }

            reader.ReadByte();   // byte de enchimento depois
         }
      }

      return bitmap;
   }

   /// <summary>Reduz mantendo a proporcao, com fundo transparente.</summary>
   private static Bitmap Fit(Bitmap source, int size)
   {
      using (source)
      {
         float scale = Math.Min((float)size / source.Width, (float)size / source.Height);
         int w = Math.Max(1, (int)(source.Width  * scale));
         int h = Math.Max(1, (int)(source.Height * scale));

         var target = new Bitmap(size, size, PixelFormat.Format32bppArgb);
         using Graphics g = Graphics.FromImage(target);
         g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
         g.DrawImage(source, (size - w) / 2, (size - h) / 2, w, h);
         return target;
      }
   }
}
