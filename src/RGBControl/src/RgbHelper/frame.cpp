#include "frame.h"

#include <algorithm>

std::vector<Packet> BuildFramePackets(int headerIndex, const uint32_t* colors, int count, uint32_t byteOrder)
{
    static const uint8_t headers[3] = {0x58, 0x59, 0x62};
    std::vector<Packet> packets;
    if (count <= 0 || headerIndex < 0 || headerIndex > 2)
        return packets;

    const int boR = (byteOrder >> 16) & 0xFF, boG = (byteOrder >> 8) & 0xFF, boB = byteOrder & 0xFF;
    int sent = 0;
    for (int k = 0; k < count;)
    {
        const int n = std::min(19, count - k);
        Packet p{};
        p[0] = 0xCC;
        p[1] = headers[headerIndex];
        p[2] = static_cast<uint8_t>(sent);
        p[3] = static_cast<uint8_t>(sent >> 8);
        p[4] = static_cast<uint8_t>(n * 3);
        for (int i = 0; i < n; i++, k++)
        {
            const int off = 5 + i * 3;
            p[off + boR] = static_cast<uint8_t>(colors[k] >> 16);
            p[off + boG] = static_cast<uint8_t>(colors[k] >> 8);
            p[off + boB] = static_cast<uint8_t>(colors[k]);
        }
        sent += n * 3;
        packets.push_back(p);
    }
    return packets;
}
