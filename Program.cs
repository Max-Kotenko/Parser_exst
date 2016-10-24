using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using AngleSharp.Parser.Html;
using AngleSharp.Dom;
using System.Net;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Data.SqlClient;
using System.Data;
using AngleSharp.Dom.Html;
using System.Runtime.Serialization.Formatters.Binary;

namespace Program
{
    //Enumerations
    public enum ResponseReturn {SingleDataReturn, MultiplyDataReturn, NoneData };
    public enum ResponseTypeOfError { Captcha, ProxyBanned, NothingFound, UnknownError };

    //Classes
    public class ArticlePartNumber
    {
        public bool IsMultiplePcodeFound { get; set; }
        public string Pcode { get; set; }
        public string CurrentCode { get; set; }
        public string pcode_OR_id { get; set; }
        public string BaseAddress { get; set; }
        public string queue_id { get; set; }
        public ArticlePartNumber()
        {
            queue_id = null;
            BaseAddress = @"http://www.exist.ru/price.aspx";
            IsMultiplePcodeFound = false;
            Pcode = null;
            CurrentCode = null;
            pcode_OR_id = "?pcode=";
        }
        public string GetURI()
        {
            return BaseAddress + pcode_OR_id + CurrentCode;
        }
    }

    public class SqlCon
    {
        public SqlConnectionStringBuilder ConnectionString { get; set; }
        public SqlConnection Connection { get; set; }
        public SqlCommand Command { get; set; }
        public SqlDataReader DataReader { get; set; }
        public string cmdText { get; set; }
        public SqlCon()
        {
            ConnectionString = new SqlConnectionStringBuilder();
            Command = new SqlCommand();
            DataReader = null;
            cmdText = null;
        }
    }

    public class HtmlParserSettings
    {
        public HttpClient httpClient { get; set; }
        public HtmlParser HTML { get; set; }

        public HtmlParserSettings()
        {
            httpClient = null;
            HTML = new HtmlParser();
        }

    }

    public static class proxyServerSettings
    {
        public static string Address { get; set; }
        public static string Port { get; set; }
        public static string UserName { get; set; }
        public static string Password { get; set; }

        public static string GetProxyAndPortSTR()
        {
            return Address + ":" + Port;
        }
    }

    public static class SaveDirectory
    {
        public static string Directory { get; set; }
    }

    public static class FormContent
    {
        public static FormUrlEncodedContent WebForms { get; set; } = null;

        //POST params sub_page
        public static string __tm_HiddenField { get; set; } = "";
        public static string __T { get; set; } = "";
        public static string __P { get; set; } = "";
        public static string __N { get; set; } = "";
        public static string __hfProducer { get; set; } = "";
        public static string __hfModel { get; set; } = "";
        public static string __hfDisableUserCar { get; set; } = "";
        public static string __VIEWSTATEGENERATOR { get; set; } = "";
        public static string __VIEWSTATE { get; set; } = "";
        //POST params main_page
        public static string __hdnPrdid { get; set; } = "";
        public static string __hdnPid { get; set; } = "";
        public static string __hdnPcode { get; set; } = "";
        public static string __EVENTVALIDATION { get; set; } = "";
    }

    public static class Existru_Login
    {
        public static string login { get; set; }
        public static string password { get; set; }
        public static string proxyId { get; set; }
        public static string isEnabled { get; set; }
        public static string isBanned { get; set; }
        public static string requestsCount { get; set; }
        public static string nextRequestTime { get; set; }
    }

    class Program
    {
            //If authorization needed
        //var user = "9859686270";
        //var pwd = "856497";

            //Pcodes and Pids for tests
        //pid = 92A06C9C
        //pcode = 3028576
        //pid = B5705107
        //pid = C86002F8
        //pcode = 4853069485
        //pcode = 50820SNBJ02
        //pid = 916023A2
        //pcode = 50890SNAA02
        //pcode = A2108803370

            //multiple issuance (множественная выдача pid)
        //pcode = 115906
        //pcode = 7701477028

        ArticlePartNumber APN = new ArticlePartNumber();
        SqlCon SQL = new SqlCon();
        HtmlParserSettings Parser = new HtmlParserSettings();
        HttpClientHandler httpClientHandler = null;


        public void Init_SQL()
        {
            //Init SQL Connection
            try
            {
                SQL.ConnectionString.DataSource = "uvk3w8cxow.database.windows.net";
                SQL.ConnectionString.InitialCatalog = "avtoproazure2";
                SQL.ConnectionString.UserID = "RdsLimited";
                SQL.ConnectionString.Password = "JaveToGheRu832480234";
                SQL.Connection = new SqlConnection(SQL.ConnectionString.ConnectionString);
            }
            catch (Exception ex)
            {
                WriteLogReport(ex, "Ошибка при инициализации SQL соединения: " + SQL.ConnectionString.ConnectionString);
                WriteMessage(ex.Message);
            }
            
        }
        public void GetValidLogin()
        {
            try
            {
                using (SQL.Command = new SqlCommand("SELECT TOP 1 * FROM existru_accounts WHERE isEnabled=1 AND nextRequestTime<GETDATE()", SQL.Connection))
                {
                    using (SQL.DataReader = SQL.Command.ExecuteReader())
                    {
                        if (SQL.DataReader.HasRows)
                        {
                            SQL.DataReader.Read();
                            Existru_Login.login = SQL.DataReader["login"].ToString();
                            Existru_Login.password = SQL.DataReader["password"].ToString();
                            Existru_Login.proxyId = SQL.DataReader["proxyId"].ToString();
                            Existru_Login.isEnabled = SQL.DataReader["isEnabled"].ToString();
                            Existru_Login.isBanned = SQL.DataReader["isBanned"].ToString();
                            Existru_Login.requestsCount = SQL.DataReader["requestsCount"].ToString();
                            Existru_Login.nextRequestTime = SQL.DataReader["nextRequestTime"].ToString();
                        }
                        else
                            throw new Exception("Аккаунты закончились");
                    }
                }
                GetValidProxy();
            }
            catch (Exception ex)
            {
                WriteLogReport(ex, "Ошибка при попытке вытащить прокси из БД");
                WriteMessage(ex.Message);
            }



        }
        public void GetValidProxy()
        {
            try
            {
                /*
                SELECT TOP 1 * FROM existru_proxies WHERE existruIsEnabled=1 AND existruNextRequestTime<GETDATE()
                 */
                using (SQL.Command = new SqlCommand("SELECT TOP 1 * FROM existru_proxies WHERE existruIsEnabled=1 AND existruNextRequestTime<GETDATE() AND id=" + Existru_Login.proxyId, SQL.Connection))
                {
                    using (SQL.DataReader = SQL.Command.ExecuteReader())
                    {
                        if (SQL.DataReader.HasRows)
                        {
                            SQL.DataReader.Read();
                            proxyServerSettings.Address = SQL.DataReader["host"].ToString();
                            proxyServerSettings.Port = SQL.DataReader["port"].ToString();
                            proxyServerSettings.UserName = SQL.DataReader["login"].ToString();
                            proxyServerSettings.Password = SQL.DataReader["password"].ToString();
                        }
                        else
                            WriteMessage("Прокси закончились");
                    }
                }
                Init_httpClient_WithProxy();
            }
            catch (Exception ex)
            {
                WriteLogReport(ex, "Ошибка при попытке вытащить прокси из БД");
                WriteMessage(ex.Message);
            }



        }
        public static void WriteCookiesToDisk(string file, CookieContainer cookieJar)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            using (Stream stream = File.Create(file))
            {
                try
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(stream, cookieJar);
                }
                catch (Exception ex)
                {
                    WriteLogReport(ex, "Problem writing cookies to disk: ");
                }
            }
        }
        public static CookieContainer ReadCookiesFromDisk(string file)
        {
            try
            {
                using (Stream stream = File.Open(file, FileMode.Open))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    return (CookieContainer)formatter.Deserialize(stream);
                }
            }
            catch (Exception ex)
            {
                WriteLogReport(ex, "Problem reading cookies from disk: ");
                return new CookieContainer();
            }
        }
        public void Init_httpClient_WithProxy()
        {
            try
            {
                string proxyUri = proxyServerSettings.GetProxyAndPortSTR();

                NetworkCredential proxyCreds = new NetworkCredential(
                    proxyServerSettings.UserName,
                    proxyServerSettings.Password
                );

                WebProxy proxy = new WebProxy(proxyUri, false)
                {
                    UseDefaultCredentials = false,
                    Credentials = proxyCreds,
                };

                // Now create a client handler which uses that proxy
                httpClientHandler = new HttpClientHandler()
                {
                    Proxy = proxy,
                    PreAuthenticate = true,
                    UseDefaultCredentials = false,
                };
                httpClientHandler.CookieContainer = ReadCookiesFromDisk(@"D:\Exist.ru_Cookies\" + Existru_Login.login + ".txt");
                Parser.httpClient = new HttpClient(httpClientHandler);
                Init_HttpClient(httpClientHandler);
            }
            catch (Exception ex)
            {
                WriteMessage("Ошибка при инициализации httpClient новым прокси");
                WriteLogReport(ex, "Ошибка при инициализации httpClient новым прокси: " + proxyServerSettings.GetProxyAndPortSTR());
            }

            
        }
        public void Log_In()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, @"http://www.exist.ru/Profile/Login.aspx");
                var formData = new List<KeyValuePair<string, string>>();
                formData.Add(new KeyValuePair<string, string>("login", Existru_Login.login));
                formData.Add(new KeyValuePair<string, string>("pass", Existru_Login.password));

                request.Content = new FormUrlEncodedContent(formData);
                Parser.httpClient.SendAsync(request).Wait();
            }
            catch (Exception ex)
            {
                WriteLogReport(ex, "Ошибка во время авторизации ");
            }
            
        }

        public void Check_IsLogin(string GetContent = null)
        {
            bool NeedTo_Login = true;
            if(GetContent == null)
                GetContent = Parser.httpClient.GetStringAsync(APN.GetURI()).Result;
            do
            {
                
                var LoginDoc = Parser.HTML.Parse(GetContent);
                var IsLogin = LoginDoc.QuerySelector("div.login");
                if (IsLogin.TextContent.Contains("Регистрация"))
                {
                    Log_In();
                    WriteCookiesToDisk(@"D:\Exist.ru_Cookies\" + Existru_Login.login + ".txt", httpClientHandler.CookieContainer);
                }
                else
                    NeedTo_Login = false;


            } while (NeedTo_Login);
        }

        public void Init_HttpClient(HttpClientHandler httpClientHandler)
        {
            try
            {
                /*httpClient.DefaultRequestHeaders
                  .Accept
                  .Add(new MediaTypeWithQualityHeaderValue("application/json"));*/
                Parser.httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application / x - www - form - urlencoded; charset = UTF - 8");
                Parser.httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/53.0.2785.143 Safari/537.36");
                var BrowserImitation = Parser.httpClient.GetAsync(@"http://www.exist.ru").Result;

                foreach (var item in BrowserImitation.Headers)
                {
                    Parser.httpClient.DefaultRequestHeaders.TryAddWithoutValidation(item.Key, item.Value);

                }
                Check_IsLogin();

            }
            catch (Exception ex)
            {
                WriteMessage("Не удалось инициализировать httpClient нужными Хедерами");
                WriteLogReport(ex, "Не удалось инициализировать httpClient нужными Хедерами: " + proxyServerSettings.GetProxyAndPortSTR());
            }
            
        }
        public bool ChangePcode()
        {
            //select top 1 * from existru_queue where isProcessed = 0
            try
            {
                APN.queue_id = null;
                bool NeedUpdate = false;
                using (SQL.Command = new SqlCommand("SELECT TOP 1 * FROM existru_queue WHERE isProcessed='0'", SQL.Connection))
                {
                    using (SQL.DataReader = SQL.Command.ExecuteReader())
                    {
                        if (SQL.DataReader.HasRows)
                        {
                            while (SQL.DataReader.Read())
                            {
                                APN.Pcode = SQL.DataReader["code"].ToString();
                                APN.queue_id = SQL.DataReader["id"].ToString();
                                NeedUpdate = true;
                            }
                        }
                        else
                        {
                            WriteMessage("queue закончились");
                            NeedUpdate = false;
                        }
                        
                    }
                }
                if (NeedUpdate)
                {
                    using (var Command = new SqlCommand("UPDATE existru_queue SET isProcessed=1  WHERE id=@queue_id", SQL.Connection))
                    {
                        Command.Parameters.Add("@queue_id", SqlDbType.Int).Value = APN.queue_id;
                        Command.ExecuteNonQuery();
                    }
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                WriteLogReport(ex, "Ошибка при попытке вытащить queue из БД");
                WriteMessage("Ошибка при попытке вытащить queue из БД: " + ex.Message);
                return false;
            }

        }
        public void AccountBanned()
        {
            try
            {
                using (SQL.Command = new SqlCommand("UPDATE existru_accounts SET nextRequestTime=DATEADD(day,1,GETDATE()) WHERE login = '" + Existru_Login.login + "'; ", SQL.Connection))
                {
                    SQL.Command.ExecuteNonQuery();
                    WriteMessage("/n/n/t/tAccountBanned " + Existru_Login.login);
                    WriteLogReport(null, "/n/n/t/tAccountBanned " + Existru_Login.login);
                }
                Queue_NotParsed();
                GetValidLogin();
            }
            catch (Exception ex)
            {
                WriteMessage(ex.Message);
            }
        }
        public void RequestsCount()
        {
            try
            {
                using (SQL.Command = new SqlCommand("UPDATE existru_proxies SET existruRequestsCount += '1' WHERE host = '" + proxyServerSettings.Address + "';", SQL.Connection))
                {
                    SQL.Command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                WriteMessage(ex.Message);
            }
        }
        public void queueParsed()
        {
            using (var Command = new SqlCommand("UPDATE existru_queue SET isProcessed=2  WHERE id=@queue_id", SQL.Connection))
            {
                Command.Parameters.Add("@queue_id", SqlDbType.Int).Value = APN.queue_id;
                Command.ExecuteNonQuery();
            }
        }
        public void Queue_NotParsed()
        {
            using (var Command = new SqlCommand("UPDATE existru_queue SET isProcessed=0  WHERE id=@queue_id", SQL.Connection))
            {
                Command.Parameters.Add("@queue_id", SqlDbType.Int).Value = APN.queue_id;
                Command.ExecuteNonQuery();
            }
        }
        static void Main(string[] args)
        {
            try
            {
                Program EPIC = new Program();
                // Initialization
                EPIC.Init_SQL();
                EPIC.SQL.Connection.Open();
                EPIC.GetValidLogin();
                //Start loop
                EPIC.MainLoop();
            }
            catch (Exception ex)
            {
                WriteMessage(ex.Message);
                WriteLogReport(ex, "Последняя инстанция.");
            }
            

            Console.ReadLine();
        }

        public ResponseReturn CheckIsValidReturn(string content)
        {
            var document = Parser.HTML.Parse(content);
            var Query = document.QuerySelector("h1");
            if (Query == null)
                return ResponseReturn.NoneData;
            else if (Query.TextContent.ToLower().Contains("найден в каталогах")) 
            {
                return ResponseReturn.MultiplyDataReturn;
            }
            else if(Query.TextContent.ToLower().Contains("список предложений"))
            {
                return ResponseReturn.SingleDataReturn;
            }
            else if (Query.TextContent.ToLower().Contains("временно нет в продаже"))
            {
                return ResponseReturn.SingleDataReturn;
            }
            else
            {
                WriteMessage("По артикулу " + APN.CurrentCode + " данных не найдено");
                return ResponseReturn.NoneData;
            }

        }

        public ResponseTypeOfError CheckTypeOfReturnedError(string content)
        {
            if(content.Contains("pageRedirect||%2fcaptcha.aspx"))
            {
                WriteMessage("\tКапча!/nМеняем прокси: " + proxyServerSettings.GetProxyAndPortSTR());
                return ResponseTypeOfError.Captcha;
            }

            var document = Parser.HTML.Parse(content);
            var Query = document.QuerySelector("span[id$='lblError']");

            if (Query != null)
            {
                if (Query.TextContent.ToLower().Contains("по вашему запросу ничего не найдено"))
                {
                    WriteMessage("По данному артикулу ничего не найдено: " + APN.CurrentCode);
                    return ResponseTypeOfError.NothingFound;
                }
                if (Query.TextContent.ToLower().Contains("ваш ip адрес заблокирован") || Query.TextContent.ToLower().Contains("превышено число запросов к базе данных в сутки"))
                {
                    WriteMessage(Query.TextContent);
                    WriteLogReport(null, Query.TextContent);
                    return ResponseTypeOfError.ProxyBanned;
                }
            }
            WriteMessage("Неизвестная ошибка: Post вернул невалидные данные и поиск по lblError не дал результатов!");
            WriteLogReport(null, "Неизвестная ошибка: Post вернул невалидные данные и поиск по lblError не дал результатов! \n " + APN.GetURI() + "  \n  " + proxyServerSettings.GetProxyAndPortSTR());
            return ResponseTypeOfError.UnknownError;
            
                


        }

        public void MainLoop()
        {
            WriteLogReport(null, "Парсин начался: " + DateTime.Now.ToShortTimeString() + Environment.NewLine);
            int counter_i = 0;
            while (counter_i++ < 100 && ChangePcode())
            {
                try
                {
                    WriteMessage("Взят pcode из списка: " + APN.Pcode + Environment.NewLine);
                    APN.pcode_OR_id = "?pcode=";
                    SaveDirectory.Directory = @"D:\Exist.ru_Login";
                    APN.CurrentCode = APN.Pcode;
                    string content = null;
                    bool SaveFile = true;
                    if (CheckIsDirectoryExists())
                    {
                        content = CheckIsFileExists();
                        if (content != null)
                            SaveFile = false;
                        //WriteMessage("Данный артикул уже парсился и сохранен");
                        //continue;
                    }
                    else
                    {
                        var GetContent = Parser.httpClient.GetStringAsync(APN.GetURI()).Result;
                        Check_IsLogin(GetContent);
                        content = PostRequest(GetContent);
                        if (content != null)
                            SaveFile = true;
                    }
                    if (content == null)
                    {
                        WriteMessage("Post запрос по Pcode не вернул валидный результат(смотри лог файл)  \n  Заменим прокси и pcode");
                        GetValidLogin();
                        continue;
                    }
                    var TypeOfReturn = CheckIsValidReturn(content);
                    if (TypeOfReturn == ResponseReturn.NoneData)
                    {// ИЗМЕНИТЬ existru_queue SET isProcessed=0
                        switch (CheckTypeOfReturnedError(content))
                        {
                            case ResponseTypeOfError.Captcha:
                            case ResponseTypeOfError.ProxyBanned:
                                AccountBanned();
                                continue;
                            case ResponseTypeOfError.NothingFound:
                                queueParsed();
                                continue;
                            case ResponseTypeOfError.UnknownError:
                                continue;
                        }
                    }
                    else if (TypeOfReturn == ResponseReturn.SingleDataReturn)
                    {//СОХРАНЯТЬ СТРАНИЦУ В HTML
                        if (SaveFile)
                        {
                            Directory.CreateDirectory(SaveDirectory.Directory);
                            SaveMainPage(content);
                        }
                        WriteMessage("Page parse start: " + APN.CurrentCode);
                    //ИЗМЕНИТЬ existru_queue SET isProcessed=2
                        ParseData_FromResponce(content);
                    }
                    else if (TypeOfReturn == ResponseReturn.MultiplyDataReturn)
                    {//СОХРАНЯТЬ СТРАНИЦУ В HTML
                        if (SaveFile)
                        {
                            Directory.CreateDirectory(SaveDirectory.Directory);
                            SaveMainPage(content);
                        }
                        var document = Parser.HTML.Parse(content);
                        var PIDs = document.QuerySelectorAll("table.tbl a");
                        WriteMessage("По данному артикулу найдено: " + PIDs.Length + " страниц");
                        WriteLogReport(null, "По данному (" + APN.Pcode + ") артикулу найдено: " + PIDs.Length + " страниц");
                        //foreach (IHtmlAnchorElement pid_code in PIDs)
                        //{
                        //    SaveDirectory.Directory = Path.Combine(@"D:\Exist.ru", APN.Pcode);
                        //    APN.CurrentCode = pid_code.Search.Replace("?pid=", "");
                        //    if(CheckIsDirectoryExists())
                        //    {
                        //        WriteMessage("Данный pid уже парсился и сохранен");
                        //        //WriteMessage("Если хотите распарсить его еще раз введите \"YES\" ");
                        //        //if (!Console.ReadLine().Contains("y"))
                        //        //    continue;
                        //    }
                        //    APN.pcode_OR_id = "?pid=";
                        //    WriteMessage("Page parse start: " + APN.CurrentCode);
                        //    ParseData_FromResponce(content);
                        //}
                    }
                }
                catch (Exception ex)
                {
                    WriteMessage("Ошибка в главном цикле программы.");
                    WriteLogReport(ex, "Ошибка в главном цикле программы.");
                }

            }
            Console.WriteLine(Environment.NewLine + "Парсинг окончен");
            WriteLogReport(null, Environment.NewLine + "Парсин окончен: " + DateTime.Now.ToShortTimeString());
            SQL.Connection.Close();
        }

        void ParseData_FromResponce(string content)
        {
            if (content == null)
                throw new Exception("Неизвестная ошибка: попытка распарсить пустую строку");

            //CREATE DATA FILE (4test)
            string filename = Path.Combine(SaveDirectory.Directory, "_(DATA).txt");
            using (var streamWriter = new StreamWriter(filename, false))
            {
                //parse some data
                var document = Parser.HTML.Parse(content);
                var isError = document.QuerySelector("div.price-data");
                if (isError != null)  //check is pcode correct
                {
                    isError.Children.ToArray()[0].Remove();     //удаляет оглавления таблицы записей
                    isError.Children.ToArray()[0].Remove();

                    int OriginalBrand = 0;
                    List<string> Brand = new List<string>();
                    foreach (var Data_Row in isError.Children)
                    {
                        if (Data_Row.ClassName.Contains("header"))
                        {
                            if (Data_Row.TextContent.Contains("Предложения по оригинальным производителям"))
                                OriginalBrand = 1;
                            else
                                OriginalBrand = 0;
                        }
                        else
                        {
                            var Query = Data_Row.QuerySelector("span.caname");
                            if (Query != null && !String.IsNullOrWhiteSpace(Query.TextContent))
                            {
                                streamWriter.WriteLine("Бренды" + Environment.NewLine);
                                Brand.Add(Query.Children[0].TextContent);
                                streamWriter.Write(Regex.Replace(Query.TextContent, @"\s+", " ") + "\t");
                                AddManufacturers(Regex.Replace(Query.TextContent, @"\s+", " ").Trim(), OriginalBrand);
                            }

                            Query = Data_Row.QuerySelector("div.description");
                            if (Query != null && !String.IsNullOrWhiteSpace(Query.TextContent))
                            {
                                streamWriter.WriteLine("Наименование" + Environment.NewLine);
                                var Tree = Regex.Replace(Query.TextContent, @"\s+", " ").Trim();
                                var Tree_New = Regex.Replace(Query.TextContent, @"[^0-9A-Za-z]",  "");
                                AddTrees(Tree, Tree_New);
                                streamWriter.Write(Tree + "\t");
                            }
                        }
                    }














                    /*



                    /////////////////////
                    // Сделать проверку, существуют ли данные элементы на странице...а то как лох парсишь все подряд
                    /////////////////////
                    var Query_Data = isError.QuerySelectorAll("span.caname");
                    if (Query_Data != null && Query_Data.Length > 0)
                    {
                        streamWriter.WriteLine("Бренды\n");
                        foreach (var item in Query_Data)
                        {
                            Brand.Add(item.Children[0].TextContent);
                            streamWriter.Write(Regex.Replace(item.TextContent, @"\s+", " ") + "\t");
                        }
                        streamWriter.WriteLine("\n");
                    }
                    Query_Data = isError.QuerySelectorAll("span.partno");
                    if (Query_Data != null && Query_Data.Length > 0)
                    {
                        streamWriter.WriteLine("pcode\n");
                        foreach (var item in Query_Data)
                        {
                            streamWriter.Write(Regex.Replace(item.TextContent, @"\s+", " ") + "\t");
                        }
                        streamWriter.WriteLine("\n");
                    }
                    Query_Data = isError.QuerySelectorAll("div.description");
                    if (Query_Data != null && Query_Data.Length > 0)
                    {
                        streamWriter.WriteLine("Наименование\n");
                        foreach (var item in Query_Data)
                        {
                            streamWriter.Write(Regex.Replace(item.TextContent, @"\s+", " ") + "\t");
                        }
                        streamWriter.WriteLine("\n");
                    }
                    Query_Data = isError.QuerySelectorAll("div.data-avail");
                    if (Query_Data != null && Query_Data.Length > 0)
                    {
                        streamWriter.WriteLine("В наличии\n");
                        foreach (var item in Query_Data)
                        {
                            try
                            {
                                streamWriter.Write(Regex.Replace(item.TextContent, "[^0-9.]", "") + "\t");
                            }
                            catch (Exception)
                            {
                                streamWriter.Write("-1");
                            }
                        }
                        streamWriter.WriteLine("\n");
                    }
                    Query_Data = isError.QuerySelectorAll("div.data-days");
                    if (Query_Data != null && Query_Data.Length > 0)
                    {
                        streamWriter.WriteLine("Ожидаемый срок поставки\n");
                        foreach (var item in Query_Data)
                        {
                            streamWriter.Write(Regex.Replace(item.TextContent, "[^0-9.]", "") + "\t");
                        }
                        streamWriter.WriteLine("\n");
                    }
                    Query_Data = isError.QuerySelectorAll("div.data-price");
                    if (Query_Data != null && Query_Data.Length > 0)
                    {
                        streamWriter.WriteLine("Цена\n");
                        foreach (var item in Query_Data)
                        {
                            streamWriter.Write(Regex.Replace(item.TextContent, "[^0-9.]", "") + "\t");
                        }
                        streamWriter.WriteLine("\n");
                    }
                    Query_Data = isError.QuerySelectorAll("div.data-pack");
                    if (Query_Data != null && Query_Data.Length > 0)
                    {
                        streamWriter.WriteLine("В упаковке\n");
                        foreach (var item in Query_Data)
                        {
                            streamWriter.Write(Regex.Replace(item.TextContent, "[^0-9.]", "") + "\t");
                        }
                        streamWriter.WriteLine("\n");
                    }
                    Query_Data = isError.QuerySelectorAll("div.informicon > a");
                    if (Query_Data != null && Query_Data.Length > 0)
                    {
                        string PCODE_FORMAT = null;
                        streamWriter.WriteLine("Ссылки\n");
                        foreach (IHtmlAnchorElement item in Query_Data)
                        {
                            streamWriter.WriteLine(item.Search + "\t"); // @"http://exist.ru/Parts/Float.aspx" +

                            string informiconSubLink = null;
                            try
                            {
                                informiconSubLink = Parser.httpClient.GetStringAsync(@"http://exist.ru/Parts/Float.aspx" + item.Search).Result;
                            }
                            catch (Exception ex)
                            {
                                WriteLogReport(ex, "Get Request(GetStringAsync) to: " + @"http://exist.ru/Parts/Float.aspx" + item.Search);
                            }
                            var doc = Parser.HTML.Parse(informiconSubLink);
                            //Получаем PCODE в вольном формате
                            var PCODE_Query = doc.QuerySelector("a[id$='_hlMainLink']");
                            if (PCODE_Query != null)
                            {
                                PCODE_FORMAT = PCODE_Query.TextContent.Replace(Brand.First() + " ", "");
                                streamWriter.WriteLine("\tPCODE_FORMAT = " + PCODE_FORMAT);
                                //SAVE HTML CODE OF THE INFO_PAGE
                                string HTMLfilename = Path.Combine(SaveDirectory.Directory, GetSafeFilename(PCODE_FORMAT + " " + Brand.First() + ".HTML"));
                                using (var PagestreamWriter = new StreamWriter(HTMLfilename, false))
                                {
                                    PagestreamWriter.WriteLineAsync(informiconSubLink).Wait();
                                }
                            }
                            Brand.RemoveAt(0);
                            //Картинка главная
                            var ImageQuery = doc.QuerySelector("a[id$='_hlMainImage']") as IHtmlAnchorElement;
                            string imgLink = null;
                            string imgDirectory = null;
                            if (ImageQuery != null)
                            {
                                try
                                {
                                    if (ImageQuery.Search == "" && ImageQuery.ChildElementCount > 0)
                                    {
                                        for (int i = 0; i < ImageQuery.ChildElementCount; i++)
                                        {
                                            if (ImageQuery.Children[i] is IHtmlImageElement)
                                            {
                                                var imgElem = ImageQuery.Children[i] as IHtmlImageElement;
                                                var tempStrLink = imgElem.Source.Replace("about", "http");
                                                tempStrLink = tempStrLink.Remove(tempStrLink.IndexOf("&Size="));
                                                imgDirectory = Path.Combine(SaveDirectory.Directory, "IMG_FOLDER_" + tempStrLink.Replace("http://img.exist.ru/img.jpg?Key=", ""));
                                                imgLink = tempStrLink + "&Size=250x250&MethodType=8"; //exist.ru/img.jpg?Key=
                                            }
                                        }
                                    }
                                    else
                                    {
                                        imgDirectory = Path.Combine(SaveDirectory.Directory, "IMG_FOLDER_" + ImageQuery.Search.Replace("?Key=", "").Replace("&Size=1600x1400&MethodType=8", ""));
                                        imgLink = @"http://img.exist.ru/img.jpg" + ImageQuery.Search.Replace("Size=1600x1400", "Size=250x250");
                                    }
                                    Stream result = Parser.httpClient.GetStreamAsync(imgLink).Result;
                                    FileInfo file = new FileInfo(imgDirectory);
                                    file.Directory.Create();
                                    using (Stream fileStream = File.Create(imgDirectory))
                                    {
                                        result.CopyTo(fileStream);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    WriteLogReport(ex, "Невозможно скачать картинку(GetStreamAsync): " + imgLink);
                                }

                            }
                            //картинки доп.
                            var ImageQueryArr = doc.QuerySelector("div.thumbs");
                            if (ImageQueryArr != null)
                            {
                                foreach (IHtmlAnchorElement item2 in ImageQueryArr.Children)
                                {
                                    try
                                    {
                                        if (item2.Search == "" && item2.ChildElementCount > 0)
                                        {
                                            for (int i = 0; i < item2.ChildElementCount; i++)
                                            {
                                                if (item2.Children[i] is IHtmlImageElement)
                                                {
                                                    var imgElem = item2.Children[i] as IHtmlImageElement;
                                                    var tempStrLink = imgElem.Source.Replace("about://img.", "");
                                                    tempStrLink = tempStrLink.Remove(tempStrLink.IndexOf("&Size="));
                                                    imgDirectory = Path.Combine(SaveDirectory.Directory, "IMG_FOLDER_" + tempStrLink);
                                                    imgLink = tempStrLink + "&Size=250x250&MethodType=8";
                                                }
                                            }
                                        }
                                        else
                                        {
                                            imgDirectory = Path.Combine(SaveDirectory.Directory, "IMG_FOLDER_" + item2.Search.Replace("?Key=", "").Replace("&Size=1600x1400&MethodType=8", ""));
                                            imgLink = @"http://img.exist.ru/img.jpg" + item2.Search.Replace("Size=1600x1400", "Size=250x250");
                                        }
                                        Stream result = Parser.httpClient.GetStreamAsync(imgLink).Result;
                                        FileInfo file = new FileInfo(imgDirectory);
                                        file.Directory.Create();
                                        using (Stream fileStream = File.Create(imgDirectory))
                                        {
                                            result.CopyTo(fileStream);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        WriteLogReport(ex, "Невозможно скачать картинку(GetStreamAsync): " + imgLink);
                                    }
                                }
                            }

                            //////////////////////
                            //         можно проверять есть ли эти слова в div.menu-tab blTab > TextContent
                            //////////////////////

                            //Параметры = _bmTabsC0
                            var ItemParams = doc.QuerySelector(".ZeForm.item_des");
                            if (ItemParams != null)
                            {
                                foreach (var item2 in ItemParams.Children)
                                {
                                    if (item2.ChildElementCount == 0)
                                    {
                                        streamWriter.Write("\n" + Regex.Replace(item.TextContent, @"\s+", " "));
                                    }
                                    else
                                    {
                                        streamWriter.Write(Regex.Replace(item2.Children[0].TextContent, @"\s+", " ") + "\t"); //Regex.Replace(item.TextContent, @"\s+", " ") + "\t"
                                        streamWriter.Write(Regex.Replace(item2.Children[1].TextContent, @"\s+", " "));
                                    }
                                }
                            }
                            //Описание = bmTabsC1
                            var ItemDesc = doc.QuerySelector("div[id$='_bmTabsC1']");
                            if (ItemDesc != null)
                                streamWriter.Write("\n\t" + Regex.Replace(ItemDesc.TextContent, @"\s+", " "));
                            //Комплекты = _bmTabsC4 
                            var Complements = doc.QuerySelector("div[id$='_bmTabsC4'] table[id$='_gvInComplect']");
                            if (Complements != null)
                            {
                                //XElement xmlTree = XElement.Parse(Complements.OuterHtml);
                                XElement xmlTree = XElement.Parse(Complements.OuterHtml.Replace("&nbsp", " "), LoadOptions.None);
                                xmlTree.Save(Path.Combine(SaveDirectory.Directory, PCODE_FORMAT + " (Комплекты).xml"));
                            }
                            //Документы = _bmTabsC5 
                            var Documents = doc.QuerySelector("div[id$='_bmTabsC5'] table[id$='_gvDocuments']");
                            if (Documents != null)
                            {
                                //XElement xmlTree = XElement.Parse(Complements.OuterHtml);
                                XElement xmlTree = XElement.Parse(Documents.OuterHtml.Replace("&nbsp", " "), LoadOptions.None);
                                xmlTree.Save(Path.Combine(SaveDirectory.Directory, PCODE_FORMAT + " (Документы).xml"));
                            }
                            //Применимости = _bmTabsC2
                            var applicability = doc.QuerySelectorAll("div[id$='_bmTabsC2']");
                            if (applicability != null)
                            {
                                var mod = doc.QuerySelector("div[id$='_updProducer']");
                                if (mod != null)
                                {
                                    //По производителям
                                    foreach (IHtmlAnchorElement Producer in mod.Children)
                                    {
                                        string Producer_name = Producer.TextContent;
                                        string applicabilitySubLink = null;
                                        try
                                        {
                                            applicabilitySubLink = Parser.httpClient.GetStringAsync(@"http://exist.ru/Parts/float.aspx" + Producer.Search).Result;
                                        }
                                        catch (Exception ex)
                                        {
                                            WriteLogReport(ex, "Применимости на экзисте(GetStringAsync): " + @"http://exist.ru/Parts/float.aspx" + Producer.Search);
                                        }
                                        var applicabilityDoc = Parser.HTML.Parse(applicabilitySubLink);
                                        var test = applicabilityDoc.QuerySelector("div[id$='_updModel']>div.mod");
                                        //По моделям
                                        foreach (IHtmlAnchorElement MethodType in test.Children)
                                        {
                                            string Model_name = MethodType.TextContent;
                                            string MethodTypeSubLink = null;
                                            try
                                            {
                                                MethodTypeSubLink = Parser.httpClient.GetStringAsync(@"http://exist.ru/Parts/float.aspx" + MethodType.Search).Result;
                                            }
                                            catch (Exception ex)
                                            {
                                                WriteLogReport(ex, "Применимости на экзисте(GetStringAsync): " + @"http://exist.ru/Parts/float.aspx" + MethodType.Search);
                                            }
                                            if (!File.Exists(Path.Combine(SaveDirectory.Directory, GetSafeFilename(PCODE_FORMAT + " " + Producer_name + " " + Model_name + ".xml"))))
                                            {
                                                var content_post = PostRequest(MethodTypeSubLink, @"http://exist.ru/Parts/float.aspx" + MethodType.Search, @"http://exist.ru/Parts/float.aspx" + MethodType.Search,
                                                                                        "ctl00$b$ctl00$updPageSize|ctl00$b$ctl00$ucPageSizer", "ctl00$b$ctl00$ucPageSizer", "100");
                                                try
                                                {
                                                    var Prime = Parser.HTML.Parse(content_post);
                                                    var xmlQuery = Prime.QuerySelector("#ctl00_b_ctl00_gvEngines");
                                                    XElement xmlTree = XElement.Parse(xmlQuery.OuterHtml);
                                                    xmlTree.Save(Path.Combine(SaveDirectory.Directory, GetSafeFilename(PCODE_FORMAT + " " + Producer_name + " " + Model_name + ".xml")));

                                                    var Query1 = Prime.QuerySelector("#ctl00_b_ctl00_ucPagerHead");
                                                    if (Query1.ChildElementCount > 0)
                                                    {           //По страницам (больше 100)
                                                        foreach (var Pager_item in Query1.Children)
                                                        {
                                                            if (Pager_item.TagName == "A")
                                                            {
                                                                var content2 = PostRequest(MethodTypeSubLink, @"http://exist.ru/Parts/float.aspx" + MethodType.Search, @"http://exist.ru/Parts/float.aspx" + MethodType.Search,
                                                                                                    "ctl00$b$ctl00$updPageNumberH|ctl00$b$ctl00$ucPagerHead", "ctl00$b$ctl00$ucPagerHead", Pager_item.TextContent);

                                                                var Prime2 = Parser.HTML.Parse(content2);
                                                                var xmlQuery2 = Prime2.QuerySelector("#ctl00_b_ctl00_gvEngines");
                                                                XElement xmlTree2 = XElement.Parse(xmlQuery2.OuterHtml);
                                                                xmlTree2.Save(Path.Combine(SaveDirectory.Directory, GetSafeFilename(PCODE_FORMAT + " " + Producer_name + " " + Model_name + "(" + Pager_item.TextContent + "_page).xml")));
                                                            }
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    WriteLogReport(ex, "Ошибка сохранения таблицы применимости в XML " + @"http://exist.ru/Parts/float.aspx" + MethodType.Search);
                                                }
                                            }
                                            else
                                                WriteMessage("File: " + GetSafeFilename(PCODE_FORMAT + " " + Producer_name + " " + Model_name + ".xml") + "\talready exists");
                                        }
                                    }
                                }
                                mod = doc.QuerySelector("iframe.iframe-catalog"); //HtmlIFrameElement
                                if (mod != null)
                                {
                                    if (!File.Exists(Path.Combine(SaveDirectory.Directory, PCODE_FORMAT + " (_InFrame).xml")))
                                    {

                                        try
                                        {
                                            var src = mod.Attributes.GetNamedItem("src");
                                            var str = Parser.httpClient.GetStringAsync(src.Value).Result;
                                            var Prime = Parser.HTML.Parse(str);
                                            var xmlQuery = Prime.QuerySelector("#tblTable");
                                            XElement xmlTree = XElement.Parse(xmlQuery.InnerHtml.Replace("&nbsp", " "), LoadOptions.None);
                                            //tblTable
                                            var ModelStr = Prime.QuerySelector("#lblModels b").TextContent;
                                            xmlTree.Save(Path.Combine(SaveDirectory.Directory, GetSafeFilename(PCODE_FORMAT + " " + ModelStr + " (_InFrame).xml")));
                                            var Query1 = Prime.QuerySelector("#lblModels");
                                            for (int i = 0; i < Query1.ChildElementCount; i++)
                                            {
                                                if (Query1.Children[i] is IHtmlAnchorElement)
                                                {
                                                    var Pager_item = Query1.Children[i] as IHtmlAnchorElement;
                                                    var NextFrameStr = Parser.httpClient.GetStringAsync(src.Value.Remove(src.Value.IndexOf("?")) + Pager_item.Search).Result;

                                                    var Prime2 = Parser.HTML.Parse(NextFrameStr);
                                                    var xmlQuery2 = Prime2.QuerySelector("#tblTable");
                                                    XElement xmlTree2 = XElement.Parse(xmlQuery2.OuterHtml.Replace("&nbsp", " "), LoadOptions.None);
                                                    xmlTree2.Save(Path.Combine(SaveDirectory.Directory, GetSafeFilename(PCODE_FORMAT + " " + Pager_item.TextContent + " (_InFrame).xml")));
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            WriteMessage(APN.Pcode + "  " + APN.CurrentCode);
                                            WriteLogReport(ex, "Ошибка сохранения таблицы применимости (Фрейм) в XML " + mod.Attributes.GetNamedItem("src").Value);
                                        }
                                    }
                                    else
                                        WriteMessage("File: " + PCODE_FORMAT + " (_InFrame).xml" + "\talready exists");
                                }
                            }
                        }
                        streamWriter.WriteLine("\n");
                    }
                    else
                    {
                        //Brand.Remove(Brand.Last());
                        if (Brand.Count > 0)
                            Brand.RemoveAt(Brand.Count - 1);
                    }
                    WriteMessage("Page parse end.\n");*/
                }
                else
                {
                    WriteLogReport(null, "Ошибка при выполнении запроса на странице: " + APN.CurrentCode + "\t div.price-data не найден.");
                }
            }
        }

        public string AddManufacturers(string Manufacturer, int isOriginal)
        {
            try
            {
                bool NeedInsert = false;
                bool NeedUpdate = false;
                string SQL_isOriginal = null;
                string SQL_ID = null;
                using (SQL.Command = new SqlCommand("SELECT TOP 1 * FROM existru_manufacturers WHERE name=@Manufacturer", SQL.Connection))
                {
                    SQL.Command.Parameters.Add("@Manufacturer", SqlDbType.NVarChar, 50).Value = Manufacturer;
                    using (SQL.DataReader = SQL.Command.ExecuteReader())
                    {
                        if (SQL.DataReader.HasRows)
                        {
                            SQL.DataReader.Read();
                            SQL_isOriginal = SQL.DataReader["isOriginal"].ToString();
                            SQL_ID = SQL.DataReader["id"].ToString();
                            if (isOriginal == 1 && SQL_isOriginal == "0")
                            {
                                NeedUpdate = true;
                            }
                        }
                        else
                        {
                            NeedInsert = true;
                        }
                    }
                }
                if(NeedInsert)
                {
                    using (var Command = new SqlCommand("INSERT INTO existru_manufacturers (name, isOriginal) output INSERTED.ID values(@Manufacturer, @isOriginal)", SQL.Connection))
                    {
                        Command.Parameters.Add("@Manufacturer", SqlDbType.NVarChar, 50).Value = Manufacturer;
                        Command.Parameters.Add("@isOriginal", SqlDbType.TinyInt).Value = isOriginal;
                        SQL_ID = Command.ExecuteScalar().ToString();
                    }
                }
                if(NeedUpdate)
                {
                    using (var Command = new SqlCommand("UPDATE existru_manufacturers SET isOriginal=1 WHERE id=N'" + SQL_ID + "'", SQL.Connection))
                    {
                        Command.ExecuteNonQuery();
                    }
                }
                return SQL_ID;
            }
            catch (Exception ex)
            {
                WriteLogReport(ex, "Ошибка при работе с таблицей existru_manufacturers в БД");
                WriteMessage("Ошибка при работе с таблицей existru_manufacturers в БД: " + ex.Message);
                return null;
            }
        }
        public string AddTrees(string Tree, string Tree_New)
        {
            try
            {
                bool NeedInsert = false;
                string SQL_ID = null;
                using (SQL.Command = new SqlCommand("SELECT TOP 1 * FROM existru_trees WHERE name_new=@Tree_New", SQL.Connection))
                {
                    SQL.Command.Parameters.Add("@Tree_New", SqlDbType.NVarChar, 255).Value = Tree_New;
                    using (SQL.DataReader = SQL.Command.ExecuteReader())
                    {
                        if (SQL.DataReader.HasRows)
                        {
                            SQL.DataReader.Read();
                            SQL_ID = SQL.DataReader["id"].ToString();
                        }
                        else
                        {
                            NeedInsert = true;
                        }
                    }
                }
                if (NeedInsert)
                {
                    using (var Command = new SqlCommand("INSERT INTO existru_trees (name, name_new) output INSERTED.ID values(@Tree, @Tree_New)", SQL.Connection))
                    {
                        Command.Parameters.Add("@Tree", SqlDbType.NVarChar, 255).Value = Tree;
                        Command.Parameters.Add("@Tree_New", SqlDbType.NVarChar, 255).Value = Tree_New;
                        SQL_ID = Command.ExecuteScalar().ToString();
                    }
                }
                return SQL_ID;
            }
            catch (Exception ex)
            {
                WriteLogReport(ex, "Ошибка при работе с таблицей existru_manufacturers в БД");
                WriteMessage("Ошибка при работе с таблицей existru_manufacturers в БД: " + ex.Message);
                return null;
            }
        }


        public bool CheckIsDirectoryExists()
        {
            try
            {
                SaveDirectory.Directory = Path.Combine(SaveDirectory.Directory, APN.CurrentCode);
                if (!Directory.Exists(SaveDirectory.Directory))
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                WriteMessage("Невозможно создать директорию: " + SaveDirectory.Directory);
                WriteLogReport(ex, "Невозможно создать директорию: " + SaveDirectory.Directory);
                return false;
            }
            
        }

        public string CheckIsFileExists()
        {
            try
            {
                if (File.Exists(Path.Combine(SaveDirectory.Directory, "_(Main_Page).HTML")))
                {
                    using (StreamReader sr = new StreamReader(Path.Combine(SaveDirectory.Directory, "_(Main_Page).HTML")))
                    {
                        //WriteMessage("File: " + APN.CurrentCode + "\\_(Main_Page).HTML" + "\talready exists");
                        return sr.ReadToEndAsync().Result;
                    }
                }
                else return null;
            }
            catch (Exception ex)
            {
                WriteLogReport(ex);
                return null;
            }
        }

        public void SaveMainPage(string content)
        {
            try
            {
                //SAVE HTML CODE
                string HTMLfilename = Path.Combine(SaveDirectory.Directory, "_(Main_Page).HTML");
                using (var streamWriter = new StreamWriter(HTMLfilename, false))
                {
                    streamWriter.WriteLineAsync(content).Wait();
                }
            }
            catch (Exception ex)
            {
                WriteMessage("Ошибка записи файла: _(Main_Page)");
                WriteLogReport(ex, "Ошибка записи файла: _(Main_Page) " + APN.GetURI());
            }
            
        }

        public void ParseWebForms(string GetResponse, bool Main_Page)
        {
            try
            {
                var document = Parser.HTML.Parse(GetResponse);
                IHtmlInputElement Query = null;

                Query = document.QuerySelector("input[id$='_HiddenField']") as IHtmlInputElement;
                FormContent.__tm_HiddenField = Query.Value;
                Query = document.QuerySelector("#__VIEWSTATEGENERATOR") as IHtmlInputElement;
                FormContent.__VIEWSTATEGENERATOR = Query.Value;
                Query = document.QuerySelector("#__VIEWSTATE") as IHtmlInputElement;
                FormContent.__VIEWSTATE = Query.Value;
                Query = document.QuerySelector("#ctl00_ctl00_b_b_hdnPrdid") as IHtmlInputElement;
                FormContent.__hdnPrdid = Query.Value;
                Query = document.QuerySelector("#ctl00_ctl00_b_b_hdnPid") as IHtmlInputElement;
                FormContent.__hdnPid = Query.Value;
                Query = document.QuerySelector("#ctl00_ctl00_b_b_hdnPcode") as IHtmlInputElement;
                FormContent.__hdnPcode = Query.Value;
                Query = document.QuerySelector("#__EVENTVALIDATION") as IHtmlInputElement;
                FormContent.__EVENTVALIDATION = Query.Value;
            }
            catch (Exception ex)
            {
                WriteLogReport(ex, "Невозможно спарсить данные в полях скрытых input'ов. \nПроверьте строку возвращенную Get запросом!\nMainPage\tpcode = " + APN.CurrentCode);
                WriteMessage(Environment.NewLine + "Невозможно спарсить данные в полях скрытых input'ов. \nПроверьте строку возвращенную Get запросом!");
            }
        }

        public void ParseWebForms(string GetResponse)
        {
            try
            {
                var document = Parser.HTML.Parse(GetResponse);
                IHtmlInputElement Query = null;

                Query = document.QuerySelector("input[id$='_HiddenField']") as IHtmlInputElement;
                FormContent.__tm_HiddenField = Query.Value;
                Query = document.QuerySelector("#__VIEWSTATEGENERATOR") as IHtmlInputElement;
                FormContent.__VIEWSTATEGENERATOR = Query.Value;
                Query = document.QuerySelector("#__VIEWSTATE") as IHtmlInputElement;
                FormContent.__VIEWSTATE = Query.Value;
                Query = document.QuerySelector("input[id$='ctl00_b_ctl00_T']") as IHtmlInputElement;
                FormContent.__T = Query.Value;
                Query = document.QuerySelector("input[id$='ctl00_b_ctl00_P']") as IHtmlInputElement;
                FormContent.__P = Query.Value;
                Query = document.QuerySelector("input[id$='ctl00_b_ctl00_N']") as IHtmlInputElement;
                FormContent.__N = Query.Value;
                Query = document.QuerySelector("#ctl00_b_ctl00_hfProducer") as IHtmlInputElement;
                FormContent.__hfProducer = Query.Value;
                Query = document.QuerySelector("#ctl00_b_ctl00_hfModel") as IHtmlInputElement;
                FormContent.__hfModel = Query.Value;
                Query = document.QuerySelector("#ctl00_b_ctl00_hfDisableUserCar") as IHtmlInputElement;
                FormContent.__hfDisableUserCar = Query.Value;
            }
            catch (Exception ex)
            {
                WriteLogReport(ex, "Невозможно спарсить данные в полях скрытых input'ов. \nПроверьте строку возвращенную Get запросом!\nSubPage\tpcode = " + APN.CurrentCode);
                WriteMessage(Environment.NewLine + "Невозможно спарсить данные в полях скрытых input'ов. \nПроверьте строку возвращенную Get запросом!");
            }
        }

        string PostRequest(string GetResponse)  //MAIN PAGE
        {
            ParseWebForms(GetResponse, true);
            HttpResponseMessage response = null;
            int i = 0;

            while (response == null && i < 3)
            {
                try
                {
                    FormContent.WebForms = new FormUrlEncodedContent(new[]
                    {
                        //WebForms
                        new KeyValuePair<string, string>("ctl00_ctl00_b_tm_HiddenField", FormContent.__tm_HiddenField),
                        new KeyValuePair<string, string>("ctl00$ctl00$b$tm", "ctl00$ctl00$b$tm|ctl00$ctl00$b$b$btnPost"),
                        new KeyValuePair<string, string>("ctl00$ctl00$b$b$hdnTimer", ""),
                        new KeyValuePair<string, string>("ctl00$ctl00$b$b$hdnSort", ""),
                        new KeyValuePair<string, string>("ctl00$ctl00$b$b$hdnPrdid", FormContent.__hdnPrdid),
                        new KeyValuePair<string, string>("ctl00$ctl00$b$b$hdnPid", FormContent.__hdnPid),
                        new KeyValuePair<string, string>("ctl00$ctl00$b$b$hdnPcode", FormContent.__hdnPcode),
                        new KeyValuePair<string, string>("__VIEWSTATEGENERATOR", FormContent.__VIEWSTATEGENERATOR),
                        new KeyValuePair<string, string>("__VIEWSTATE", FormContent.__VIEWSTATE),
                        new KeyValuePair<string, string>("__EVENTVALIDATION", FormContent.__EVENTVALIDATION),
                        new KeyValuePair<string, string>("__EVENTTARGET", "ctl00$ctl00$b$b$btnPost"),
                        new KeyValuePair<string, string>("__EVENTARGUMENT", ""),
                        new KeyValuePair<string, string>("__ASYNCPOST", "true")
                    });
                    
                    response = Parser.httpClient.PostAsync(APN.GetURI(), FormContent.WebForms).Result;
                    if(response.StatusCode != HttpStatusCode.OK)
                    {
                        WriteMessage("Post Request: " + response.StatusCode);
                        response.EnsureSuccessStatusCode();
                    }
                    WriteMessage("Запрос выполнен успешно: " + APN.CurrentCode);
                    RequestsCount();
                    i = 3;
                }
                catch (Exception ex)
                {
                    WriteLogReport(null, "Пост запрос к серверу не удался\nMain_Page: " + APN.GetURI());
                    WriteLogReport(ex, response.RequestMessage + Environment.NewLine);
                    WriteLogReport(null, response.StatusCode + Environment.NewLine);
                    WriteLogReport(null, FormContent.WebForms.ToString() + Environment.NewLine);
                    i++;
                    WriteMessage(ex.Message);
                    response = null;
                    System.Threading.Thread.Sleep(1000);
                }
            }
            if (response == null)
                return null;
            return response.Content.ReadAsStringAsync().Result;
        } 

        string PostRequest(string GetResponse,string Post_To, string Referer, string __tm, string __EVENTTARGET, string __EVENTARGUMENT)    //SUB PAGE 
        {
            ParseWebForms(GetResponse);
            HttpResponseMessage response = null;
            int i = 0;
            while (response == null && i < 3)
            {
                try
                {
                    FormContent.WebForms = new FormUrlEncodedContent(new[]
                    {
                        //WebForms
                        new KeyValuePair<string, string>("ctl00_b_ctl00_tm_HiddenField", FormContent.__tm_HiddenField),
                        new KeyValuePair<string, string>("ctl00_b_ctl00_T", FormContent.__T),
                        new KeyValuePair<string, string>("ctl00_b_ctl00_P", FormContent.__P),
                        new KeyValuePair<string, string>("ctl00_b_ctl00_N", FormContent.__N),
                        new KeyValuePair<string, string>("ctl00_b_ctl00_hfProducer", FormContent.__hfProducer),
                        new KeyValuePair<string, string>("ctl00_b_ctl00_hfModel", FormContent.__hfModel),
                        new KeyValuePair<string, string>("ctl00_b_ctl00_hfDisableUserCar", FormContent.__hfDisableUserCar),
                        new KeyValuePair<string, string>("__VIEWSTATE", FormContent.__VIEWSTATE),
                        new KeyValuePair<string, string>("__VIEWSTATEGENERATOR", FormContent.__VIEWSTATEGENERATOR),
                        new KeyValuePair<string, string>("ctl00$b$ctl00$tm", __tm),                                    // параметры (пагинация или страница вывода)
                        new KeyValuePair<string, string>("__EVENTTARGET", __EVENTTARGET),                             // "ucPageSizer" or "ucPagerHead"
                        new KeyValuePair<string, string>("__EVENTARGUMENT", __EVENTARGUMENT),                        // 100 (кол-во вывода или страница)
                        new KeyValuePair<string, string>("__BOOKMARKERbmTabs", "2"),
                        new KeyValuePair<string, string>("__ASYNCPOST", "true")
                    });
                    Parser.httpClient.DefaultRequestHeaders.Referrer = null;
                    Parser.httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Referer", Referer);

                    response = Parser.httpClient.PostAsync(Post_To, FormContent.WebForms).Result;
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        WriteMessage("Post Request: " + response.StatusCode);
                        response.EnsureSuccessStatusCode();
                    }
                    WriteMessage("Запрос выполнен успешно: " + APN.CurrentCode);
                    //RequestsCount();
                    i = 3;
                }
                catch (Exception ex)
                {

                    WriteLogReport(null, "Пост запрос к серверу не удался\nSub_Page: " + APN.GetURI());
                    WriteLogReport(ex, response.RequestMessage + Environment.NewLine);
                    WriteLogReport(null, response.StatusCode + Environment.NewLine);
                    WriteLogReport(null, FormContent.WebForms.ToString() + Environment.NewLine);
                    i++;
                    WriteMessage(ex.Message);
                    response = null;
                    System.Threading.Thread.Sleep(1000);
                }
            }
            if (response == null)
                return null;
            return response.Content.ReadAsStringAsync().Result;
        }  

        public static string GetSafeFilename(string filename)
        {

            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));

        }
        
        static void WriteLogReport(Exception ex, string additionalInfo = null)
        {
            try
            {
                using (var streamWriter = new StreamWriter(@"D:\log_login.txt", true))
                {
                    if (additionalInfo != null)
                        streamWriter.WriteLine("\t" + additionalInfo);
                    if (ex != null)
                    {
                        streamWriter.WriteLine(ex.ToString());
                    }
                }
            }
            catch (Exception Excpt)
            {
                WriteMessage(Environment.NewLine + "Exception при попытке записать Exception в Log ??? " + Excpt.Message + Environment.NewLine);
            }
            
        }

        static void WriteMessage(string str)
        {
            Console.WriteLine(str);
        }

        /*static void GetDataRecurs(IHtmlCollection<IElement> Children, StreamWriter streamWriter)
        {
            if (Children.Length > 0)
            {
                foreach (var item in Children)
                {
                    if (item.ClassName == "data-row" && item.Children[0].ClassName != "desc-row")
                        continue;
                    if (item.TextContent.Length > 0)
                    {
                        if (item.ChildElementCount == 0 && (item.TextContent != " " && item.TextContent != "~"))// &nbsp;/пробел/~
                        {
                            if (item.ClassName != null)
                                streamWriter.WriteLine(item.ClassName + " :" + item.TextContent + "\n");
                            else
                                streamWriter.WriteLine(item.ParentElement.ClassName + " :" + item.TextContent + "\n");
                        }
                        else
                            GetDataRecurs(item.Children, streamWriter);
                    }
                    else if (item.ChildElementCount == 0 && item.ParentElement.TextContent.Length > 0)
                    {
                        streamWriter.WriteLine(item.ParentElement.ClassName + " :" + item.ParentElement.TextContent + "\n");
                        return;
                    }
                }
            }
            else return;
        }*/

    }

}



