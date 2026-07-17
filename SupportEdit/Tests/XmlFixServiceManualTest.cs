using System;
using SupportEdit.Services;

namespace SupportEdit.Tests
{
    // Manual test helper for XmlFixService, run from app or a small console wrapper if needed
    public static class XmlFixServiceManualTest
    {
        public static void RunAll()
        {
            TestAddMinutes();
            TestReplaceNgayThYlVariants();
            TestNguoiThucHienOverwrite();
            Console.WriteLine("Manual tests finished.");
        }

        private static void TestAddMinutes()
        {
            var r1 = XmlFixService.AddMinutesToNgayYl("202605210834", 10);
            Console.WriteLine(r1 == "202605210844" ? "OK 1" : "FAIL 1: " + r1);
            var r2 = XmlFixService.AddMinutesToNgayYl("202605210855", 10);
            Console.WriteLine(r2 == "202605210905" ? "OK 2" : "FAIL 2: " + r2);
        }

        private static void TestReplaceNgayThYlVariants()
        {
            var xml = "<ROOT><CHI_TIET_DVKT><STT>1</STT><NGAY_YL>202605210834</NGAY_YL><NGAY_TH_YL /></CHI_TIET_DVKT></ROOT>";
            var res = XmlFixService.FixNgayThYl(xml, 1, 1, false);
            Console.WriteLine(res.RequireOverwriteConfirmation ? "NEED_CONFIRM" : (res.Success ? "OK replacevariant1" : "FAIL"));

            xml = "<ROOT><CHI_TIET_DVKT><STT>1</STT><NGAY_YL>202605210834</NGAY_YL><NGAY_TH_YL></NGAY_TH_YL></CHI_TIET_DVKT></ROOT>";
            res = XmlFixService.FixNgayThYl(xml, 1, 1, false);
            Console.WriteLine(res.RequireOverwriteConfirmation ? "NEED_CONFIRM" : (res.Success ? "OK replacevariant2" : "FAIL"));
        }

        private static void TestNguoiThucHienOverwrite()
        {
            var xml = "<ROOT><CHI_TIET_DVKT><STT>2</STT><NGUOI_THUC_HIEN /></CHI_TIET_DVKT></ROOT>";
            var res = XmlFixService.FixNguoiThucHien(xml, 1, 2, "CODE1", false);
            Console.WriteLine(res.Success ? "OK set nguoi" : "FAIL set nguoi");

            xml = "<ROOT><CHI_TIET_DVKT><STT>2</STT><NGUOI_THUC_HIEN>OLD</NGUOI_THUC_HIEN></CHI_TIET_DVKT></ROOT>";
            res = XmlFixService.FixNguoiThucHien(xml, 1, 2, "CODE1", false);
            Console.WriteLine(res.RequireOverwriteConfirmation ? "NEED_CONFIRM_NGUOI" : "FAIL_EXPECT_CONFIRM");
            res = XmlFixService.FixNguoiThucHien(xml, 1, 2, "CODE1", true);
            Console.WriteLine(res.Success ? "OK overwrite nguoi" : "FAIL overwrite nguoi");
        }
    }
}
