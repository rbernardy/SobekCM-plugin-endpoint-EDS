using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using SobekCM.Core.Aggregations;
using SobekCM.Core.Configuration.Engine;
using SobekCM.Core.FileSystems;
using SobekCM.Core.Results;
using SobekCM.Engine_Library.ApplicationState;
using SobekCM.Engine_Library.Endpoints;
using SobekCM.Tools;

namespace EbscoServices
{
    public class EbscoServices : ResultsServices
    {
        /// <summary> Get just the search statistics information for a search or browse </summary>
        /// <param name="Response"></param>
        /// <param name="UrlSegments"></param>
        /// <param name="QueryString"></param>
        /// <param name="Protocol"></param>
        /// <param name="IsDebug"></param>
        public void EBSCO_Results_XML(HttpResponse Response, List<string> UrlSegments, NameValueCollection QueryString, Microservice_Endpoint_Protocol_Enum Protocol, bool IsDebug)
        {
            Custom_Tracer tracer = new Custom_Tracer();
            tracer.Add_Trace("EbscoServices.Get_Search_Results_Set", "Parse request to determine search requested");

            // Get all the searh field necessary from the query string
            Results_Arguments args = new Results_Arguments(QueryString);

            // Additional results arguments
            // limit number of results
            int artificial_result_limitation = -1;
            Boolean isNumeric = false;

            if (!String.IsNullOrEmpty(QueryString["limit_results"]))
            {
                isNumeric=Int32.TryParse(QueryString["limit_results"], out artificial_result_limitation);

                if (!isNumeric)
                {
                    artificial_result_limitation = -1;
                }
                else if (artificial_result_limitation<1)
                {
                    artificial_result_limitation = -1;
                }
            }

            int pagenum = 1;

            if (!String.IsNullOrEmpty(QueryString["page"]))
            {
                isNumeric = Int32.TryParse(QueryString["page"], out pagenum);

                if (!isNumeric)
                {
                    pagenum = 1;
                }
                else if (pagenum < 1)
                {
                    pagenum = 1;
                }
                else if (pagenum>1)
                {
                    artificial_result_limitation = -1;
                }
            }

            // limit title length, in words
            int artificial_title_length_limitation = -1;

            if (!String.IsNullOrEmpty(QueryString["title_length"]))
            {
                isNumeric=Int32.TryParse(QueryString["title_length"], out artificial_title_length_limitation);

                if (!isNumeric)
                {
                    artificial_title_length_limitation = -1;
                }
                else if (artificial_title_length_limitation<1)
                {
                    artificial_title_length_limitation = -1;
                }
            }

            Boolean include_metadata = false;

            if (!String.IsNullOrEmpty(QueryString["metadata"]))
            {
                include_metadata = true;
            }

            // Was a collection indicated?
            if (UrlSegments.Count > 0)
            {
                args.Aggregation = UrlSegments[0];
            }

            // Get the aggregation object (we need to know which facets to use, etc.. )
            tracer.Add_Trace("EbscoServices.Get_Search_Results_Set", "Get the '" + args.Aggregation + "' item aggregation (for facets, etc..)");
            Complete_Item_Aggregation aggr = AggregationServices.get_complete_aggregation(args.Aggregation, true, tracer);

            // If no aggregation was returned, that is an error
            if (aggr == null)
            {
                tracer.Add_Trace("EbscoServices.Get_Search_Results_Set", "Returned aggregation was NULL... aggregation code may not be valid");

                if (IsDebug)
                {
                    Response.ContentType = "text/plain";
                    Response.Output.WriteLine("DEBUG MODE DETECTED");
                    Response.Output.WriteLine();
                    Response.Output.WriteLine(tracer.Text_Trace);
                    return;
                }

                Response.ContentType = "text/plain";
                Response.Output.WriteLine("Error occurred or aggregation '" + args.Aggregation + "' not valid");
                Response.StatusCode = 500;
                return;
            }

            // Perform the search
            tracer.Add_Trace("EbscoServices.Get_Search_Results_Set", "Perform the search");
            Search_Results_Statistics resultsStats;
            List<iSearch_Title_Result> resultsPage;
            ResultsEndpointErrorEnum error = Get_Search_Results(args, aggr, false, tracer, out resultsStats, out resultsPage);
            
            // Was this in debug mode?
            // If this was debug mode, then just write the tracer
            if (IsDebug)
            {
                tracer.Add_Trace("EbscoServices.Get_Search_Results_Set", "Debug mode detected");

                Response.ContentType = "text/plain";
                Response.Output.WriteLine("DEBUG MODE DETECTED");
                Response.Output.WriteLine();
                Response.Output.WriteLine(tracer.Text_Trace);
                return;
            }

            try
            {
                tracer.Add_Trace("EbscoServices.Get_Search_Results_Set", "Begin writing the XML result to the response");
                Response.Output.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\" ?>");

                int count_pages = (int)Math.Floor((double)resultsStats.Total_Items / 20);

                if (count_pages==0)
                {
                    count_pages=1;
                }

                if (pagenum > count_pages)
                {
                    pagenum = count_pages;
                }

                Response.Output.WriteLine("<results total_items=\"" + resultsStats.Total_Items + "\" total_titles=\"" + resultsStats.Total_Titles + "\" page_count=\"" + count_pages + "\" max_results_per_page=\"20\"");

                if (artificial_result_limitation != -1)
                {
                    Response.Output.WriteLine("limit_results=\"" + artificial_result_limitation + "\"");
                }
               
                if (artificial_title_length_limitation!=-1)
                {
                    Response.Output.WriteLine("title_length=\"" + artificial_title_length_limitation + "\"");
                }

                Response.Output.WriteLine(">");

                // Map to the results object title / item
                tracer.Add_Trace("ResultsServices.Get_Search_Results_Set", "Map to the results object title / item");

                // resultnum
                int resultnum = 0;

                if (pagenum>1)
                {
                    resultnum = ((pagenum - 1) * 20);
                }

                if (resultsPage != null)
                {
                    foreach (iSearch_Title_Result thisResult in resultsPage)
                    {
                        // Every results should have an item
                        if (thisResult.Item_Count == 0)
                        {
                            continue;
                        }
                        else
                        {
                            resultnum++;
                        }

                        if (artificial_result_limitation!=-1 && resultnum>artificial_result_limitation)
                        {
                            tracer.Add_Trace("EbscoServices.Get_Search_Results_Set", "Reached limit [" + artificial_result_limitation + "].");
                            break;
                        }

                        // add each descriptive field over
                        iSearch_Item_Result itemResult = thisResult.Get_Item(0);

                        string bibid = thisResult.BibID;
                        string title = thisResult.GroupTitle;
                        string vid = itemResult.VID;
                        string thumbnail = itemResult.MainThumbnail;

                        title = Process_Title(title, artificial_title_length_limitation);
                        
                        Response.Output.WriteLine("  <result resultnum=\"" + resultnum + "\" bibid=\"" + bibid + "\" vid=\"" + vid + "\">");
                        Response.Output.WriteLine("    <title>" + HttpUtility.HtmlEncode(title) + "</title>");
                        Response.Output.WriteLine("    <url_item>" + Engine_ApplicationCache_Gateway.Settings.Servers.Application_Server_URL + bibid + "/" + vid + "/</url_item>");

                        if (!String.IsNullOrEmpty(thumbnail))
                        {
                            try
                            {
                                Response.Output.WriteLine("    <url_thumbnail>" + Engine_ApplicationCache_Gateway.Settings.Servers.Image_URL +
                                                          SobekFileSystem.AssociFilePath(bibid, vid).Replace("\\", "/") + thumbnail.Trim() + "</url_thumbnail>");
                            }
                            catch ( Exception ee )
                            {
                                Response.Output.WriteLine("ERROR WRITING THUMBNAIL");
                                Response.Output.WriteLine(ee.Message);
                                Response.Output.WriteLine(ee.StackTrace);
                            }
                        }

                        int field_index = 0;

                        if (resultsStats.Metadata_Labels.Count > 0 && include_metadata)
                        {
                            Response.Output.WriteLine("<metadata>");

                            foreach (string metadataTerm in resultsStats.Metadata_Labels)
                            {
                                if (!String.IsNullOrWhiteSpace(thisResult.Metadata_Display_Values[field_index]))
                                {
                                    // how to display this metadata field?
                                    string metadataTermDisplay = metadataTerm.ToLower();
                                    string termString = thisResult.Metadata_Display_Values[field_index];

                                    if (termString.IndexOf("|") > 0)
                                    {
                                        string[] splitter = termString.Split("|".ToCharArray());

                                        foreach (string thisSplit in splitter)
                                        {
                                            if (!String.IsNullOrWhiteSpace(thisSplit))
                                            {
                                                Response.Output.WriteLine("    <" + metadataTermDisplay + ">" + HttpUtility.HtmlEncode(thisSplit.Trim()) + "</" + metadataTermDisplay + ">");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Response.Output.WriteLine("    <" + metadataTermDisplay + ">" + HttpUtility.HtmlEncode(termString.Trim()) + "</" + metadataTermDisplay + ">");
                                    }
                                }

                                field_index++;
                            }

                            Response.Output.WriteLine("</metadata>");
                        }

                        Response.Output.WriteLine("  </result>");
                    }
                }

                Response.Output.WriteLine("</results>");

                tracer.Add_Trace("EbscoServices.Get_Search_Results_Set", "Done writing the XML result to the response");
            }
            catch (Exception ee)
            {
                Response.Output.Write(ee.Message);
                Response.Output.Write(ee.StackTrace);
            }

            // If an error occurred, return the error
            switch (error)
            {
                case ResultsEndpointErrorEnum.Database_Exception:
                    Response.ContentType = "text/plain";
                    Response.Output.WriteLine("Database exception");
                    Response.StatusCode = 500;
                    return;

                case ResultsEndpointErrorEnum.Database_Timeout_Exception:
                    Response.ContentType = "text/plain";
                    Response.Output.WriteLine("Database timeout");
                    Response.StatusCode = 500;
                    return;

                case ResultsEndpointErrorEnum.Solr_Exception:
                    Response.ContentType = "text/plain";
                    Response.Output.WriteLine("Solr exception");
                    Response.StatusCode = 500;
                    return;

                case ResultsEndpointErrorEnum.Unknown:
                    Response.ContentType = "text/plain";
                    Response.Output.WriteLine("Unknown error");
                    Response.StatusCode = 500;
                    return;
            }
        }

        /// <summary> Get just the search statistics information for a search or browse </summary>
        /// <param name="Response"></param>
        /// <param name="UrlSegments"></param>
        /// <param name="QueryString"></param>
        /// <param name="Protocol"></param>
        /// <param name="IsDebug"></param>
        public void EBSCO_Results_JSON(HttpResponse Response, List<string> UrlSegments, NameValueCollection QueryString, Microservice_Endpoint_Protocol_Enum Protocol, bool IsDebug)
        {
            Custom_Tracer tracer = new Custom_Tracer();
            tracer.Add_Trace("EbscoServices.EBSCO_Results_JSON", "Parse request to determine search requested");

            // Get all the searh field necessary from the query string
            Results_Arguments args = new Results_Arguments(QueryString);

            // Additional results arguments
            // limit number of results
            int artificial_result_limitation = -1;
            Boolean isNumeric = false;

            if (!String.IsNullOrEmpty(QueryString["limit_results"]))
            {
                isNumeric=Int32.TryParse(QueryString["limit_results"], out artificial_result_limitation);

                if (!isNumeric)
                {
                    artificial_result_limitation = -1;
                }
                else if (artificial_result_limitation < 1)
                {
                    artificial_result_limitation = -1;
                }
            }

            int pagenum = 1;

            if (!String.IsNullOrEmpty(QueryString["page"]))
            {
                isNumeric = Int32.TryParse(QueryString["page"], out pagenum);

                if (!isNumeric)
                {
                    pagenum = 1;
                }
                else if (pagenum < 1)
                {
                    pagenum = 1;
                }
                else if (pagenum > 1)
                {
                    artificial_result_limitation = -1;
                }
            }

            // limit title length, in words
            int artificial_title_length_limitation = -1;

            if (!String.IsNullOrEmpty(QueryString["title_length"]))
            {
                isNumeric=Int32.TryParse(QueryString["title_length"], out artificial_title_length_limitation);

                if (!isNumeric)
                {
                    artificial_title_length_limitation = -1;
                }
                else if (artificial_title_length_limitation < 1)
                {
                    artificial_title_length_limitation = -1;
                }
            }

            Boolean include_metadata = false;

            if (!String.IsNullOrEmpty(QueryString["metadata"]))
            {
                include_metadata = true;
            }

            // Was a collection indicated?
            if (UrlSegments.Count > 0)
            {
                args.Aggregation = UrlSegments[0];
            }

            // Get the aggregation object (we need to know which facets to use, etc.. )
            tracer.Add_Trace("EbscoServices.EBSCO_Results_JSON", "Get the '" + args.Aggregation + "' item aggregation (for facets, etc..)");
            Complete_Item_Aggregation aggr = AggregationServices.get_complete_aggregation(args.Aggregation, true, tracer);

            // If no aggregation was returned, that is an error
            if (aggr == null)
            {
                tracer.Add_Trace("EbscoServices.EBSCO_Results_JSON", "Returned aggregation was NULL... aggregation code may not be valid");

                if (IsDebug)
                {
                    Response.ContentType = "text/plain";
                    Response.Output.WriteLine("DEBUG MODE DETECTED");
                    Response.Output.WriteLine();
                    Response.Output.WriteLine(tracer.Text_Trace);
                    return;
                }

                Response.ContentType = "text/plain";
                Response.Output.WriteLine("Error occurred or aggregation '" + args.Aggregation + "' not valid");
                Response.StatusCode = 500;
                return;
            }

            // Perform the search
            tracer.Add_Trace("EbscoServices.EBSCO_Results_JSON", "Perform the search");
            Search_Results_Statistics resultsStats;
            List<iSearch_Title_Result> resultsPage;
            ResultsEndpointErrorEnum error = Get_Search_Results(args, aggr, false, tracer, out resultsStats, out resultsPage);


            // Was this in debug mode?
            // If this was debug mode, then just write the tracer
            if (IsDebug)
            {
                Response.ContentType = "text/plain";
                Response.Output.WriteLine("DEBUG MODE DETECTED");
                Response.Output.WriteLine();
                Response.Output.WriteLine(tracer.Text_Trace);
                return;
            }

            Response.Output.WriteLine("{\"stats\":{\"total_items\":\"" + resultsStats.Total_Items + "\",\"total_titles\":\"" + resultsStats.Total_Titles + "\"},");
            Response.Output.WriteLine(" \"results\":[");

            // Map to the results object title / item
            tracer.Add_Trace("ResultsServices.EBSCO_Results_JSON", "Map to the results object title / item");
            int items_counter = 0;
            int resultnum = 0;

            if (resultsPage != null)
            {
                foreach (iSearch_Title_Result thisResult in resultsPage)
                {
                    // Every results should have an item
                    if (thisResult.Item_Count == 0)
                    {
                        continue;
                    }
                    else
                    {
                        resultnum++;
                    }

                    if (artificial_result_limitation != -1 && resultnum > artificial_result_limitation)
                    {
                        tracer.Add_Trace("EbscoServices.Get_Search_Results_Set", "Reached limit [" + artificial_result_limitation + "].");
                        break;
                    }
                    // Was this NOT the first item?
                    if (items_counter > 0)
                    {
                        Response.Output.WriteLine(",");
                    }

                    Response.Output.Write("        ");
                    items_counter++;

                    // add each descriptive field over
                    iSearch_Item_Result itemResult = thisResult.Get_Item(0);

                    string bibid = thisResult.BibID;
                    string title = thisResult.GroupTitle;
                    string vid = itemResult.VID;
                    string thumbnail = itemResult.MainThumbnail;

                    title = Process_Title(title, artificial_title_length_limitation);

                    // {"bibid":"1212", "vid":"00001", "title":"sdsd", "subjects":["subj1", "subj2", "subj3"] },

                    Response.Output.Write("{ \"bibid\":\"" + bibid + "\", \"vid\":\"" + vid + "\", ");
                    Response.Output.Write("\"title\":\"" + HttpUtility.HtmlEncode(title) + "\",");
                    Response.Output.Write("\"url_item\":\"" + Engine_ApplicationCache_Gateway.Settings.Servers.Application_Server_URL + bibid + "/" + vid + "/\",");
                    Response.Output.Write("\"url_thumbnail\":\"" + Engine_ApplicationCache_Gateway.Settings.Servers.Image_URL +
                                              SobekFileSystem.AssociFilePath(bibid, vid).Replace("\\", "/") + thumbnail + "\"");

                    int field_index = 0;

                    if (resultsStats.Metadata_Labels.Count > 0 && include_metadata)
                    {
                        foreach (string metadataTerm in resultsStats.Metadata_Labels)
                        {
                            if (!String.IsNullOrWhiteSpace(thisResult.Metadata_Display_Values[field_index]))
                            {
                                // how to display this metadata field?
                                string metadataTermDisplay = metadataTerm.ToLower();

                                string termString = thisResult.Metadata_Display_Values[field_index];
                                Response.Output.Write(",\"" + metadataTermDisplay + "\":[");

                                int individual_term_counter = 0;

                                if (termString.IndexOf("|") > 0)
                                {
                                    string[] splitter = termString.Split("|".ToCharArray());

                                    foreach (string thisSplit in splitter)
                                    {
                                        if (!String.IsNullOrWhiteSpace(thisSplit))
                                        {
                                            if (individual_term_counter > 0)
                                                Response.Output.Write(", \"" + HttpUtility.HtmlEncode(thisSplit.Trim()) + "\"");
                                            else
                                                Response.Output.Write("\"" + HttpUtility.HtmlEncode(thisSplit.Trim()) + "\"");

                                            individual_term_counter++;
                                        }
                                    }
                                }
                                else
                                {
                                    Response.Output.Write("\"" + HttpUtility.HtmlEncode(termString.Trim()) + "\"");
                                }

                                Response.Output.Write("]");
                            }

                            field_index++;
                        }
                    }

                    Response.Output.Write("}");
                }
            }

            Response.Output.WriteLine();
            Response.Output.WriteLine("    ]");
            Response.Output.WriteLine("} ");

            // If an error occurred, return the error
            switch (error)
            {
                case ResultsEndpointErrorEnum.Database_Exception:
                    Response.ContentType = "text/plain";
                    Response.Output.WriteLine("Database exception");
                    Response.StatusCode = 500;
                    return;

                case ResultsEndpointErrorEnum.Database_Timeout_Exception:
                    Response.ContentType = "text/plain";
                    Response.Output.WriteLine("Database timeout");
                    Response.StatusCode = 500;
                    return;

                case ResultsEndpointErrorEnum.Solr_Exception:
                    Response.ContentType = "text/plain";
                    Response.Output.WriteLine("Solr exception");
                    Response.StatusCode = 500;
                    return;

                case ResultsEndpointErrorEnum.Unknown:
                    Response.ContentType = "text/plain";
                    Response.Output.WriteLine("Unknown error");
                    Response.StatusCode = 500;
                    return;
            }
        }

        private String Process_Title(String title,int artificial_title_length_limitation)
        {
            int title_word_count = 0;

            // Remove standard library cataloging brackets if present
            if (title.StartsWith("[") && title.EndsWith("]"))
            {
                //tracer.Add_Trace("EbscoServices.Get_Search_Results_Set", "Title was wrapped with square brackets, removed.");
                title = title.Substring(1, title.Length - 2);
            }

            // process title word limit if set
            if (artificial_title_length_limitation != -1)
            {
                //tracer.Add_Trace("EbscoServices.Get_Search_Results_Set", "Title word limit was set [" + artificial_title_length_limitation + "].");
                String[] words = title.Split(' ');

                if (words.Length >1)
                {
                    if (artificial_title_length_limitation > words.Length)
                    {
                        title_word_count = words.Length;
                    }
                    else
                    {
                        title_word_count = artificial_title_length_limitation;
                    }

                    title = "";

                    int counter = 0;

                    foreach (String word in words)
                    {
                        title += word.Trim() + " ";

                        counter++;

                        if (counter > title_word_count)
                        {
                            break;
                        }
                    }
                }
            }

            title = title.Trim().Replace("  ", " ");

            return title;
        }
    }
}