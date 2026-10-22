using SPTarkov.Server.Core.Models.Common;

namespace ItemInfo.Caches.Models;

public record struct HeadsetInfo(
    MongoId TemplateId,
    double AmbientVolume,
    double CompressorAttack,
    double CompressionGain,
    double CompressorThreshold,
    double Distortion
);
