﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace TestOnvif
{
    /// <summary>
    /// Класс для обработки потока RTP пакетов, в которых содержатся изображения JPEG
    /// </summary>
    public class RFC2435Handler
    {
        const int BUFFER_SIZE = 1000000;
        /// <summary>
        /// Буфер для склеивания полноценного JPEGа
        /// </summary>
        private byte[] buffer = new byte[BUFFER_SIZE];

        /// <summary>
        /// Смещение к первой таблице квантования
        /// </summary>
        private const int QUANT_1_OFFSET = 25;

        /// <summary>
        /// Смещение ко второй таблице квантования
        /// </summary>
        private const int QUANT_2_OFFSET = 94;

        /// <summary>
        /// Смещение к полю высоты изображения
        /// </summary>
        private const int HEIGHT_OFFSET = 163;

        /// <summary>
        /// Смещение к полю ширины изображения
        /// </summary>
        private const int WIDTH_OFFSET = 165;

        /// <summary>
        /// Смещение к данным изображения
        /// </summary>
        private const int DATA_OFFSET = 623;

        #region JPEG header
        private static byte[] jpegHeader =
        {
#region SOI marker
            0xFF, 0xD8,                                     // код маркера
#endregion
#region APP0 marker
            0xFF, 0xE0,                                     // код маркера 
            0x00, 0x10,                                     // длина маркера
            0x4A, 0x46, 0x49, 0x46, 0x00,                   // строка JFIF\0
            0x01, 0x01,                                     // номер версии
            0x01,                                           // единицы измерений
            0x00, 0x60,                                     // плотность пикселей по горизонтали
            0x00, 0x60,                                     // плотность пикселей по вертикали
            0x00, 0x00,                                     // возможные данные превью
#endregion
#region DQT marker (таблица квантования 1)
            0xFF, 0xDB,                                     // код маркера
            0x00, 0x43,                                     // длина маркера
            0x00,                                           // биты 0..3: номер таблицы (0..3, в противном случае ошибка),
                                                            // биты 4..7: точность таблицы, 0 = 8 бит, в противном случае 16 бит
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // \    offset 25
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // |
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // |
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, //  > данные таблицы квантования 1, 64 байта
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // |
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // |
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // |
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // /
#endregion
#region DQT marker (таблица квантования 2)
            0xFF, 0xDB,                                     // код маркера
            0x00, 0x43,                                     // длина маркера
            0x01,                                           // биты 0..3: номер таблицы (0..3, в противном случае ошибка),
                                                            // биты 4..7: точность таблицы, 0 = 8 бит, в противном случае 16 бит
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // \    offset 94
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // |
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // |
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, //  > данные таблицы квантования 2, 64 байта
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // |
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // |
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // |
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // /
#endregion
#region SOF0 marker
            0xFF, 0xC0,                                     // код маркера
            0x00, 0x11,                                     // длина маркера
            0x08,                                           // точность
            0x00, 0x00,                                     // высота изображения   offset 163
            0x00, 0x00,                                     // ширина изображения   offset 165
            0x03,                                           // количество компонент
            0x01, 0x22, 0x00,                               // данные для
            0x02, 0x11, 0x01,                               // каждого
            0x03, 0x11, 0x01,                               // компонента
#endregion
#region DHT marker (таблица Хаффмана 1)
            0xFF, 0xC4,                                     // код маркера
            0x00, 0x1F,                                     // длина маркера
            0x00, 0x00, 0x01, 0x05, 0x01, 0x01, 0x01, 0x01, // \
            0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, //  > данные таблицы Хаффмана 1
            0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, // |
            0x07, 0x08, 0x09, 0x0A, 0x0B,                   // /
            
#endregion
#region DHT marker (таблица Хаффмана 2)
            0xFF, 0xC4,                                     // код маркера
            0x00, 0xB5,                                     // длина маркера
            0x10, 0x00, 0x02, 0x01, 0x03, 0x03, 0x02, 0x04, // \
            0x03, 0x05, 0x05, 0x04, 0x04, 0x00, 0x00, 0x01, // |
            0x7D, 0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, // |
            0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, // |
            0x07, 0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, // |
            0x08, 0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52, 0xD1, // |
            0xF0, 0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0A, // |
            0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, // |
            0x28, 0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, // |
            0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, // |
            0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, // |
            0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, //  > данные таблицы Хаффмана 2
            0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, // |
            0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, // |
            0x89, 0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, // |
            0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, // |
            0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, // |
            0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, // |
            0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, // |
            0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, // |
            0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, // |
            0xEA, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, // |
            0xF8, 0xF9, 0xFA,                               // /
#endregion
#region DHT marker (таблица Хаффмана 3)
            0xFF, 0xC4,                                     // код маркера
            0x00, 0x1F,                                     // длина маркера
            0x01, 0x00, 0x03, 0x01, 0x01, 0x01, 0x01, 0x01, // \
            0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, //  > данные таблицы Хаффмана 3
            0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, // |
            0x07, 0x08, 0x09, 0x0A, 0x0B,                   // /
#endregion
#region DHT marker (таблица Хаффмана 4)
            0xFF, 0xC4,                                     // код маркера
            0x00, 0xB5,                                     // длина маркера
            0x11, 0x00, 0x02, 0x01, 0x02, 0x04, 0x04, 0x03, // \
            0x04, 0x07, 0x05, 0x04, 0x04, 0x00, 0x01, 0x02, // |
            0x77, 0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, // |
            0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, // |
            0x71, 0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, // |
            0x91, 0xA1, 0xB1, 0xC1, 0x09, 0x23, 0x33, 0x52, // |
            0xF0, 0x15, 0x62, 0x72, 0xD1, 0x0A, 0x16, 0x24, // |
            0x34, 0xE1, 0x25, 0xF1, 0x17, 0x18, 0x19, 0x1A, // |
            0x26, 0x27, 0x28, 0x29, 0x2A, 0x35, 0x36, 0x37, // |
            0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, // |
            0x48, 0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, // |
            0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, //  > данные таблицы Хаффмана 4
            0x68, 0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, // |
            0x78, 0x79, 0x7A, 0x82, 0x83, 0x84, 0x85, 0x86, // |
            0x87, 0x88, 0x89, 0x8A, 0x92, 0x93, 0x94, 0x95, // |
            0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, // |
            0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, // |
            0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, // |
            0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, // |
            0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, // |
            0xDA, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, // |
            0xE9, 0xEA, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, // |
            0xF8, 0xF9, 0xFA,                               // /
#endregion
#region SOS marker
            0xFF, 0xDA,                                     // код маркера
            0x00, 0x0C,                                     // длина маркера
            0x03, 0x01, 0x00, 0x02, 0x11, 0x03, 0x11, 0x00, // всякая
            0x3F, 0x00,                                     // херня
#endregion
        };
        #endregion

        /// <summary>
        /// Насколько сейчас заполнен буфер.
        /// </summary>
        private int bufferOffset;

        public RFC2435Handler()
        {
            Buffer.BlockCopy(jpegHeader, 0, buffer, 0, jpegHeader.Length);
            bufferOffset = jpegHeader.Length;
        }

        /// <summary>
        /// RTP Payload Format for JPEG-compressed Video
        /// http://tools.ietf.org/html/rfc2435
        /// </summary>
        /// <param name="packet">RTP пакет для обработки</param>
        unsafe public void HandleRtpPacket(RtpPacket packet)
        {
            int payloadLength = packet.PayloadLength;

            int offset = 0;

            byte typeSpecific = *(byte*)(packet.Payload + offset); 
            offset++;

            int fragmentOffset = BigEndian.ReadInt24((byte*)(packet.Payload + offset));
            offset += 3;   
  
            if (expectingOffset != fragmentOffset)
                corrupted = true;

            byte type = *(byte*)(packet.Payload + offset);               
            offset++;

            byte q = *(byte*)(packet.Payload + offset);
            offset++;

            ushort width = (ushort)( (*(byte*)(packet.Payload + offset)) * 8);
            offset++;

            ushort height = (ushort)( (*(byte*)(packet.Payload + offset)) * 8); 
            offset++; 

            if (type >= 64 && type <= 127)
                offset += 4; 

            byte qmbz = 0, qprecizion = 0;
            ushort qlength = 0;

            if (q >= 128 && q <= 255 && fragmentOffset == 0)
            {
                qmbz = *(byte*)(packet.Payload + offset);
                offset ++;

                qprecizion = *(byte*)(packet.Payload + offset);
                offset ++;

                qlength = BigEndian.ReadUInt16((void*)(packet.Payload + offset));
                offset += 2; 

                if (qlength == 128)
                {
                    BigEndian.WriteUInt16(buffer, HEIGHT_OFFSET, height);
                    BigEndian.WriteUInt16(buffer, WIDTH_OFFSET, width);

                    Marshal.Copy(packet.Payload + offset, buffer, QUANT_1_OFFSET, 64);
                    offset += 64;

                    Marshal.Copy(packet.Payload + offset, buffer, QUANT_2_OFFSET, 64);
                    offset += 64;
                }
            }

            int jpgLength = payloadLength - offset;

            Marshal.Copy(packet.Payload + offset, buffer, DATA_OFFSET + fragmentOffset, jpgLength);
            bufferOffset = DATA_OFFSET + fragmentOffset + jpgLength;

            expectingOffset = fragmentOffset + jpgLength;

            if (packet.IsMarker)
            {
                if (!corrupted)
                {
                    IntPtr jpg = Marshal.AllocHGlobal(bufferOffset);
                    Marshal.Copy(buffer, 0, jpg, bufferOffset);

                    currentPacketTimeStamp = packet.Timestamp;

                    if (currentPacketTimeStamp <= lastPacketTimeStamp)
                        Logger.Write(String.Format("{0}<={1}", currentPacketTimeStamp, lastPacketTimeStamp), EnumLoggerType.DebugLog);

                    JpegFrameReceived(jpg, bufferOffset, true, packet.Timestamp);

                    lastPacketTimeStamp = currentPacketTimeStamp;

                    Marshal.FreeHGlobal(jpg);
                }
                corrupted = false;
                expectingOffset = 0;
            }
        }
        uint currentPacketTimeStamp = 0;
        uint lastPacketTimeStamp = 0;

        /// <summary>
        /// Ожидаемое значение поля смещения в следующем пакете
        /// </summary>
        private int expectingOffset = 0;

        /// <summary>
        /// Флаг, сигнализирующий о том, что произошел сбой при склеивании JPEGа
        /// </summary>
        private bool corrupted = false;

        /// <summary>
        /// Событие вызывается при формировании не поврежденного JPEG-изображения
        /// </summary>
        public event JpegFrameReceivedHandler JpegFrameReceived;

        /// <summary>
        /// Тип обработчика для события формирования пакета
        /// </summary>
        /// <param name="ptr">Указатель на данные</param>
        /// <param name="count">Количество данных в байтах</param>
        public delegate void JpegFrameReceivedHandler(IntPtr data, int count, bool key, uint pts);
    }
}