﻿using MoeLoaderDelta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace SitePack
{
    public class SiteSankaku : AbstractImageSite
    {
        private SiteBooru booru;
        private SessionClient Sweb = new SessionClient();
        private SessionHeadersCollection shc = new SessionHeadersCollection();
        private Random rand = new Random();
        private string[] user = { "girltmp", "mload006", "mload107", "mload482", "mload367", "mload876", "mload652", "mload740", "mload453", "mload263", "mload395" };
        private string[] pass = { "girlis2018", "moel006", "moel107", "moel482", "moel367", "moel876", "moel652", "moel740", "moel453", "moel263", "moel395" };
        private string sitePrefix, tempuser, temppass, tempappkey, ua, pageurl;
        private static string cookie = string.Empty;

        public override string SiteUrl => $"https://{sitePrefix}.sankakucomplex.com";
        public override string SiteName => $"{sitePrefix}.sankakucomplex.com";
        public override string ShortName => sitePrefix.Contains("chan") ? "chan.sku" : "idol.sku";
        public override bool IsSupportScore => false;
        public override bool IsSupportCount => true;
        //public override string Referer => $"{SiteUrl}/post";
        public override string SubReferer => "*";

        /// <summary>
        /// sankakucomplex site
        /// </summary>
        public SiteSankaku(string prefix)
        {
            shc.Timeout = 16000;
            sitePrefix = prefix;
            CookieRestore();
        }

        /// <summary>
        /// 取页面源码 来自官方APP处理方式
        /// </summary>
        /// <param name="page"></param>
        /// <param name="count"></param>
        /// <param name="keyWord"></param>
        /// <param name="proxy"></param>
        /// <returns></returns>
        public override string GetPageString(int page, int count, string keyWord, IWebProxy proxy)
        {
            if (sitePrefix == "chan")
            {
                ua = "SCChannelApp/2.4 (Android; black)";
            }
            else if (sitePrefix == "idol")
            {
                ua = "SCChannelApp/2.3 (Android; idol)";
            }
            else
                return null;

            Login(proxy);
            return booru.GetPageString(page, count, keyWord, proxy);
        }

        public override List<Img> GetImages(string pageString, IWebProxy proxy)
        {
            return booru.GetImages(pageString, proxy);
        }

        public override List<TagItem> GetTags(string word, IWebProxy proxy)
        {
            List<TagItem> re = new List<TagItem>();

            //https://chan.sankakucomplex.com/tag/autosuggest?tag=*****&locale=en
            string url = string.Format(SiteUrl + "/tag/autosuggest?tag={0}", word);
            shc.ContentType = SessionHeadersValue.AcceptAppJson;
            string json = Sweb.Get(url, proxy, shc);
            object[] array = (new JavaScriptSerializer()).DeserializeObject(json) as object[];
            string name = "", count = "";

            if (array.Count() > 1)
            {
                if (array[1].GetType().FullName.Contains("Object[]"))
                {
                    int i = 2;
                    foreach (object names in array[1] as object[])
                    {
                        name = names.ToString();
                        count = array[i].ToString();
                        i++;
                        re.Add(new TagItem() { Name = name, Count = count });
                    }
                }
            }

            return re;
        }

        /// <summary>
        /// 还原Cookie
        /// </summary>
        private void CookieRestore()
        {
            if (!string.IsNullOrWhiteSpace(cookie)) return;

            string subdomain = sitePrefix.Contains("chan") ? "capi-beta" : "iapi";

            string ck = Sweb.GetURLCookies(SiteUrl);
            cookie = string.IsNullOrWhiteSpace(ck) ? string.Empty : $"{subdomain}.sankaku;{ck}";
        }

        /// <summary>
        /// 这破站用API需要登录！(╯‵□′)╯︵┻━┻
        /// 两个图站的账号还不通用(╯‵□′)╯︵┻━┻
        /// </summary>
        /// <param name="proxy"></param>
        private void Login(IWebProxy proxy)
        {

            string subdomain = sitePrefix.Substring(0, 1),
                loginhost = "https://";

            subdomain += subdomain.Contains("c") ? "api-beta" : "api";
            loginhost += $"{subdomain}.sankakucomplex.com";

            if (string.IsNullOrWhiteSpace(cookie) || !cookie.Contains(subdomain + ".sankaku"))
            {
                try
                {
                    cookie = string.Empty;
                    int index = rand.Next(0, user.Length);
                    tempuser = user[index];
                    temppass = GetSankakuPwHash(pass[index]);
                    tempappkey = GetSankakuAppkey(tempuser);
                    string post = cookie;

                    if (subdomain.Contains("capi"))
                        post = "user[name]=" + tempuser + "&user[password]=" + pass[index] + "&appkey=" + tempappkey;
                    else
                        post = "login=" + tempuser + "&password_hash=" + temppass + "&appkey=" + tempappkey;

                    //Post登录取Cookie
                    shc.UserAgent = ua;
                    shc.Referer = Referer;
                    shc.Accept = SessionHeadersValue.AcceptAppJson;
                    shc.ContentType = SessionHeadersValue.ContentTypeFormUrlencoded;
                    Sweb.Post(loginhost + "/user/authenticate.json", post, proxy, shc);
                    cookie = Sweb.GetURLCookies(loginhost);

                    if (sitePrefix == "idol" && !cookie.Contains("sankakucomplex_session"))
                        throw new Exception("获取登录Cookie失败");
                    else
                    {
                        cookie = subdomain + ".sankaku;" + cookie;
                    }

                    pageurl = loginhost + "/post/index.json?login=" + tempuser + "&password_hash="
                        + temppass + "&appkey=" + tempappkey + "&page={0}&limit={1}&tags={2}";


                    //登录成功才能初始化Booru类型站点
                    shc.Referer = Referer;
                    booru = new SiteBooru(SiteUrl, pageurl, null, SiteName, ShortName, false, BooruProcessor.SourceType.JSONSku, shc);
                }
                catch (Exception e)
                {
                    throw new Exception("自动登录失败: " + e.Message);
                }
            }
        }

        /// <summary>
        /// 计算用于登录等账号操作的AppKey
        /// </summary>
        /// <param name="user">用户名</param>
        /// <returns></returns>
        private static string GetSankakuAppkey(string user)
        {
            return SHA1("sankakuapp_" + user.ToLower() + "_Z5NE9YASej", Encoding.Default).ToLower();
        }

        /// <summary>
        /// 计算密码sha1
        /// </summary>
        /// <param name="password">密码</param>
        /// <returns></returns>
        private static string GetSankakuPwHash(string password)
        {
            return SHA1("choujin-steiner--" + password + "--", Encoding.Default).ToLower();
        }

        /// <summary>
        /// SHA1加密
        /// </summary>
        /// <param name="content">字符串</param>
        /// <param name="encode">编码</param>
        /// <returns></returns>
        private static string SHA1(string content, Encoding encode)
        {
            try
            {
                System.Security.Cryptography.SHA1 sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider();
                byte[] bytes_in = encode.GetBytes(content);
                byte[] bytes_out = sha1.ComputeHash(bytes_in);
                string result = BitConverter.ToString(bytes_out);
                result = result.Replace("-", string.Empty);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("SHA1Error:" + ex.Message);
            }
        }
    }
}
