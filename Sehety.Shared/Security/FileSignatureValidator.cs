using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Text;

namespace S2S.Shared.Security
{
	public static class FileSignatureValidator
	{
		private const int HeaderLength = 16;
		private static readonly byte[] EbmlHeader = { 0x1A, 0x45, 0xDF, 0xA3 };
		private static readonly byte[] OggHeader = { 0x4F, 0x67, 0x67, 0x53 };
		private static readonly byte[] RiffHeader = { 0x52, 0x49, 0x46, 0x46 };
		private static readonly byte[] FtypHeader = { 0x66, 0x74, 0x79, 0x70 };
		private static readonly byte[] MpegPsHeader = { 0x00, 0x00, 0x01, 0xBA };
		private static readonly byte[] MpegSequenceHeader = { 0x00, 0x00, 0x01, 0xB3 };
		private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
		private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF };

		public static bool IsAllowedAudio(IFormFile file, string extension)
		{
			var header = ReadHeader(file);
			if (header.Length < 4)
			{
				return false;
			}

			return NormalizeExtension(extension) switch
			{
				".mp3" => IsMp3(header),
				".wav" => HasRiffType(header, "WAVE"),
				".m4a" => HasFtyp(header),
				".mp4" => HasFtyp(header),
				".ogg" => HasHeader(header, OggHeader),
				".webm" => HasHeader(header, EbmlHeader),
				".mpeg" => IsMpegProgramStream(header) || IsMp3(header),
				_ => false
			};
		}

		public static bool IsAllowedVideo(IFormFile file, string extension)
		{
			var header = ReadHeader(file);
			if (header.Length < 4)
			{
				return false;
			}

			return NormalizeExtension(extension) switch
			{
				".mp4" => HasFtyp(header),
				".m4v" => HasFtyp(header),
				".mov" => HasFtyp(header),
				".webm" => HasHeader(header, EbmlHeader),
				".mkv" => HasHeader(header, EbmlHeader),
				".avi" => HasRiffType(header, "AVI "),
				_ => false
			};
		}

		public static bool IsAllowedImage(IFormFile file, string extension)
		{
			var header = ReadHeader(file);
			if (header.Length < 4)
			{
				return false;
			}

			return NormalizeExtension(extension) switch
			{
				".png" => HasHeader(header, PngHeader),
				".jpg" => HasHeader(header, JpegHeader),
				".jpeg" => HasHeader(header, JpegHeader),
				_ => false
			};
		}

		private static string NormalizeExtension(string extension)
		{
			return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.Trim().ToLowerInvariant();
		}

		private static byte[] ReadHeader(IFormFile file)
		{
			var buffer = new byte[HeaderLength];
			try
			{
				using var stream = file.OpenReadStream();
				var read = stream.Read(buffer, 0, buffer.Length);
				if (read <= 0)
				{
					return Array.Empty<byte>();
				}

				return read == buffer.Length ? buffer : buffer.Take(read).ToArray();
			}
			catch
			{
				return Array.Empty<byte>();
			}
		}

		private static bool HasHeader(byte[] header, byte[] signature)
		{
			if (header.Length < signature.Length)
			{
				return false;
			}

			for (var i = 0; i < signature.Length; i++)
			{
				if (header[i] != signature[i])
				{
					return false;
				}
			}

			return true;
		}

		private static bool HasFtyp(byte[] header)
		{
			if (header.Length < 12)
			{
				return false;
			}

			return header[4] == FtypHeader[0]
				&& header[5] == FtypHeader[1]
				&& header[6] == FtypHeader[2]
				&& header[7] == FtypHeader[3];
		}

		private static bool HasRiffType(byte[] header, string riffType)
		{
			if (header.Length < 12 || !HasHeader(header, RiffHeader))
			{
				return false;
			}

			var tagBytes = Encoding.ASCII.GetBytes(riffType);
			return header[8] == tagBytes[0]
				&& header[9] == tagBytes[1]
				&& header[10] == tagBytes[2]
				&& header[11] == tagBytes[3];
		}

		private static bool IsMp3(byte[] header)
		{
			if (header.Length >= 3 && header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33)
			{
				return true;
			}

			return header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0;
		}

		private static bool IsMpegProgramStream(byte[] header)
		{
			return HasHeader(header, MpegPsHeader) || HasHeader(header, MpegSequenceHeader);
		}
	}
}
