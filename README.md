# SobekCM-plugin-endpoint-EDS
<p>SobekCM-plugin-endpoint-EDS is a plugin for the open-source <a target="_blank" href="https://github.com/MarkVSullivan/SobekCM-Web-Application/">SobekCM Digital Repository</a> software (<a target="_blank" href="https://github.com/MarkVSullivan">Mark V. Sullivan</a>, lead developer). It provides a REST API endpoint to support a dynamic search request from the EBSCO Discovery Service&#8482; to include search results from an individual SobekCM-based repository in the its search results.</p>
<p>Initial development of this plugin began in collaboration with SobekCM's lead developer Mark V. Sullivan during an excellent training session he provided to me in December 2016.</p>
<p>The endpoint URL is http://[repository hostname]/engine/plugins/ebsco/[return type]/?t=[search terms].</p>
<p>The available return types are XML and JSON [case-insensitive].</p> 
<p>Other query string attributes:</p>
<ul>
<li>limit_results: limit the number of search results [positive integer]. If not used all results (up to 20) are returned in the first page.</li>
<li>title_length: limit the number of words returned for each result title [positive integer]. If not used the entire title will be retrieved.</li>
<li>metadata: include descriptive metadata [any value]. If not used the descriptive metadata will not appear.</li> 
<li>page: By default SobekCM returns a maximum of 20 results per page. If not used the first page of results is returned. If the page attribute is included it cancels use of any limit_results attribute value.</li>
</ul>
<p>Regardless of what query string attributes are used search result stats are returned (depending upon return type), including total items and total titles.</p>
