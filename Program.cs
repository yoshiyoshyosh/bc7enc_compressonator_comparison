using System.Diagnostics;
using NetVips;
using ssimulacra2.NET;
using Compressonator.NET;
using Hardware.Info;

string[] files = [
	"carrots", "colorpatch", "flowers", "helicopter", "noise_extreme_2", "noise_extreme",
	"noise_slight", "normal_avalisuit", "paperclips", "rainbow", "sap_body", "shanghai",
	"squares", "texture_avalisuit", "wings", "Grass001_4K-PNG_Color", "Grass001_4K-PNG_NormalGL",
	"PavingStones150_4K-PNG_Color", "PavingStones150_4K-PNG_NormalGL"
];

var hwinfo = new HardwareInfo();
hwinfo.RefreshCPUList();
int threads = hwinfo.CpuList[0].CpuCoreList.Count;
Console.WriteLine($"Using {threads} threads for Compressonator & bc7enc");

CMP_CompressOptions comp_opts = new();
comp_opts.numThreads = (uint)threads;

unsafe double ssim(byte[] refpng, byte[] dist, uint w, uint h, int ow, int oh, CMP_FORMAT format)
{
	byte[] o = new byte[w * h * 3];
	CMP_Texture comptex = new();
	comptex.width = w;
	comptex.height = h;
	comptex.format = format;
	comptex.CalculateDataSize();
	CMP_Texture decomptex = new();
	decomptex.CopyDimensionsFrom(comptex);
	decomptex.format = CMP_FORMAT.RGB_888;
	decomptex.CalculateDataSize();
	fixed (byte* ip = new Span<byte>(dist)) {
		fixed (byte* op = new Span<byte>(o)) {
			nint p1 = comptex.data;
			nint p2 = decomptex.data;
			comptex.data = new IntPtr(ip);
			decomptex.data = new IntPtr(op);
			SDK_NativeMethods.CMP_ConvertTexture(comptex, decomptex, comp_opts);
			comptex.data = p1;
			decomptex.data = p2;
		}
	}
	using var vimg = Image.NewFromMemory(o, (int)w, (int)h, 3, Enums.BandFormat.Uchar);
	using var vimg2 = vimg.Crop(0, 0, ow, oh);
	byte[] opng = vimg2.WriteToBuffer(".png[compression=1]");
	return Ssimulacra2.ComputeFromMemory(refpng, opng);
}

unsafe {

foreach (var name in files) {
	var ipath = "input/" + name + ".png";
	using var vimg = Image.NewFromFile(ipath);
	using var vimg3 = (vimg.HasAlpha()) ? vimg : vimg.AddAlpha();
	using var vimg2 = vimg3.Cast(Enums.BandFormat.Uchar, true);
	var w = (uint)vimg2.Width;
	var h = (uint)vimg2.Height;
	var sw = new Stopwatch();
	using CMP_Texture itex = new();
	itex.width = w;
	itex.height = h;
	itex.pitch = 4 * w;
	itex.format = CMP_FORMAT.RGBA_8888;
	itex.CalculateDataSize();
	using CMP_Texture otex = new();
	otex.width = itex.width;
	otex.height = itex.height;
	otex.pitch = 3 * w;

	byte[] input = vimg2.WriteToMemory();
	byte[] inputpng = vimg2.WriteToBuffer(".png[compression=1]");
	// 1Bpp for bc3, bc7, .5Bpp for bc1
	uint bw = ((w + 3) & ~3u);
	uint bh = ((h + 3) & ~3u);
	uint sz = bw * bh;
	byte[] comp_bc1 = new byte[sz / 2];
	byte[] comp_bc3 = new byte[sz];
	byte[] comp_bc7 = new byte[sz];

	Console.WriteLine($"=== {name}.png ({w}x{h}) ===");
	double score;

	var bc7enc_params = new bc7enc.NET.Types.BC7EncParams();

	sw.Reset();
	bc7enc_params.TargetFormat = bc7enc.NET.Types.EncodeFormat.BC1;
	sw.Start();
	byte[] bc7enc_bc1 = bc7enc.NET.Functions.EncodePixels(input, w, h, bc7enc_params);
	sw.Stop();
	score = ssim(inputpng, bc7enc_bc1, bw, bh, (int)w, (int)h, CMP_FORMAT.BC1);
	Console.WriteLine($"BC1 (bc7enc, 18): {sw.Elapsed.TotalSeconds} | {score}");

	sw.Reset();
	bc7enc_params.BC13_Quality = 14;
	sw.Start();
	bc7enc_bc1 = bc7enc.NET.Functions.EncodePixels(input, w, h, bc7enc_params);
	sw.Stop();
	score = ssim(inputpng, bc7enc_bc1, bw, bh, (int)w, (int)h, CMP_FORMAT.BC1);
	Console.WriteLine($"BC1 (bc7enc, 14): {sw.Elapsed.TotalSeconds} | {score}");

	sw.Reset();
	bc7enc_params.TargetFormat = bc7enc.NET.Types.EncodeFormat.BC3;
	bc7enc_params.BC13_Quality = 18;
	sw.Start();
	byte[] bc7enc_bc3 = bc7enc.NET.Functions.EncodePixels(input, w, h, bc7enc_params);
	sw.Stop();
	score = ssim(inputpng, bc7enc_bc3, bw, bh, (int)w, (int)h, CMP_FORMAT.BC3);
	Console.WriteLine($"BC3 (bc7enc, 18): {sw.Elapsed.TotalSeconds} | {score}");

	sw.Reset();
	bc7enc_params.BC13_Quality = 14;
	sw.Start();
	bc7enc_bc3 = bc7enc.NET.Functions.EncodePixels(input, w, h, bc7enc_params);
	sw.Stop();
	score = ssim(inputpng, bc7enc_bc3, bw, bh, (int)w, (int)h, CMP_FORMAT.BC3);
	Console.WriteLine($"BC3 (bc7enc, 14): {sw.Elapsed.TotalSeconds} | {score}");

	sw.Reset();
	bc7enc_params.TargetFormat = bc7enc.NET.Types.EncodeFormat.BC7;
	sw.Start();
	byte[] bc7enc_bc7 = bc7enc.NET.Functions.EncodePixels(input, w, h, bc7enc_params);
	sw.Stop();
	score = ssim(inputpng, bc7enc_bc7, bw, bh, (int)w, (int)h, CMP_FORMAT.BC7);
	Console.WriteLine($"BC7 (bc7enc): {sw.Elapsed.TotalSeconds} | {score}");

	sw.Reset();
	bc7enc_params.BC7_Perceptual = true;
	sw.Start();
	byte[] bc7enc_bc7_perceptual = bc7enc.NET.Functions.EncodePixels(input, w, h, bc7enc_params);
	sw.Stop();
	score = ssim(inputpng, bc7enc_bc7_perceptual, bw, bh, (int)w, (int)h, CMP_FORMAT.BC7);
	Console.WriteLine($"BC7 (bc7enc_perceptual): {sw.Elapsed.TotalSeconds} | {score}");

	fixed (byte* i = new Span<byte>(input)) {
		nint p1 = itex.data;
		itex.data = new IntPtr(i);

		sw.Reset();
		otex.format = CMP_FORMAT.BC1;
		otex.CalculateDataSize();
		comp_opts.quality = 1f;
		fixed (byte* o = new Span<byte>(comp_bc1)) {
			nint p2 = otex.data;
			otex.data = new IntPtr(o);
			sw.Start();
			var err = SDK_NativeMethods.CMP_ConvertTexture(itex, otex, comp_opts);
			sw.Stop();
			if (err != CMP_ERROR.CMP_OK) {
				throw new Exception("Couldn't convert: " + err);
			}
			score = ssim(inputpng, comp_bc1, bw, bh, (int)w, (int)h, CMP_FORMAT.BC1);
			Console.WriteLine($"BC1 (Compressonator, 1.0): {sw.Elapsed.TotalSeconds} | {score}");
			otex.data = p2;
		}

		sw.Reset();
		otex.format = CMP_FORMAT.BC3;
		otex.CalculateDataSize();
		fixed (byte* o = new Span<byte>(comp_bc3)) {
			nint p2 = otex.data;
			otex.data = new IntPtr(o);
			sw.Start();
			var err = SDK_NativeMethods.CMP_ConvertTexture(itex, otex, comp_opts);
			sw.Stop();
			if (err != CMP_ERROR.CMP_OK) {
				throw new Exception("Couldn't convert: " + err);
			}
			score = ssim(inputpng, comp_bc3, bw, bh, (int)w, (int)h, CMP_FORMAT.BC3);
			Console.WriteLine($"BC3 (Compressonator, 1.0): {sw.Elapsed.TotalSeconds} | {score}");
			otex.data = p2;
		}

		sw.Reset();
		otex.format = CMP_FORMAT.BC7;
		otex.CalculateDataSize();
		comp_opts.quality = 0.25f;
		fixed (byte* o = new Span<byte>(comp_bc7)) {
			nint p2 = otex.data;
			otex.data = new IntPtr(o);
			sw.Start();
			var err = SDK_NativeMethods.CMP_ConvertTexture(itex, otex, comp_opts);
			sw.Stop();
			if (err != CMP_ERROR.CMP_OK) {
				throw new Exception("Couldn't convert: " + err);
			}
			score = ssim(inputpng, comp_bc7, bw, bh, (int)w, (int)h, CMP_FORMAT.BC7);
			Console.WriteLine($"BC7 (Compressonator, 0.25): {sw.Elapsed.TotalSeconds} | {score}");
			otex.data = p2;
		}

		itex.data = p1;
	}
}

}
