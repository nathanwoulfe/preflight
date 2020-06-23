(() => {

    function preflightService($http, umbRequestHelper) {

        const urlBase = Umbraco.Sys.ServerVariables.Preflight.ApiPath;

        const helpText = `
            <p>If your content is too difficult for your visitors to read, you're all going to have a bad time.</p>
            <p>The readability test runs your content through the Flesch reading ease algorithm to determine text complexity.</p>
            <h5>The algorithm</h5>
            <p><code>RE = 206.835 - (1.015 x ASL) - (84.6 x ASW)</code></p>
            <p>Where <code>RE</code> is Readability Ease, <code>ASL</code> is Average Sentence Length, and <code>ASW</code> is Average Syllables per Word</p>
            <p>The result is a number between 0 and 100, where a higher score means better readability, with a score between 60 and 69 largely considered acceptable.</p>
            <h5>Readability test results</h5>
            <p>As well as the Flesch score, the readability test returns sentence length; average syllables per word; and long or complex words;</p>`;

        const request = (method, url, data) =>
            umbRequestHelper.resourcePromise(
                method === 'GET' ? $http.get(url) : $http.post(url, data),  
                'Something broke'
            );

        const service = {
            check: (id, culture) => request('GET', `${urlBase}check/${id}/${culture || 'default'}`),            

            checkDirty: data => request('POST', `${urlBase}checkdirty/`, data),            

            getSettings: (culture, fallback) => request('GET', `${urlBase}getSettings/1/${culture || 'default'}/${fallback}`),            

            getSettingValue: (culture, alias) => request('GET', `${urlBase}getSettingValue/${alias}/${culture || 'default'}`),

            saveSettings: (settings, tabs, culture, message = '') => request('POST', `${urlBase}saveSettings`, {
                settings, 
                tabs,
                culture,
                message
            }),

            getHelpText: () => helpText
        };

        return service;
    }

    angular.module('preflight.services').service('preflightService', ['$http', 'umbRequestHelper', preflightService]);

})();