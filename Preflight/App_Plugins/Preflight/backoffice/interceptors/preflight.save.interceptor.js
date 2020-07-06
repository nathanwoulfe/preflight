(() => {

    const postSaveUrl = '/umbracoapi/content/postsave';

    function interceptor(notificationsService, $q, $injector) {

        const checkGroup = userGroupOptInOut => {
            // use the stored value to get the corresponding key from the setting's prevalues (which is value,key paring of all groups)
            var enabledGroups = userGroupOptInOut.prevalues.filter(x => userGroupOptInOut.value.includes(x.value)).map(x => x.key);

            $injector.invoke(['authResource', authResource => {
                authResource.getCurrentUser()
                    .then(currentUser => {
                        if (enabledGroups.some(x => currentUser.userGroups.includes(x))) {
                            notificationsService.add({
                                key: 'preflight_notice',
                                view: `${Umbraco.Sys.ServerVariables.Preflight.PluginPath}/views/warning.notification.html`
                            });
                        }
                    });
            }]);
        };

        return {
            //request: request => {
            //    if (request.url.toLowerCase().indexOf(postSaveUrl) !== -1) {
            //        $injector.invoke(['preflightService', s => {
            //            s.getSettings()
            //                .then(resp => {                   
            //                    const settings = resp.data.settings;
            //                    const runOnSave = settings.find(x => x.alias === 'runPreflightOnSave'); 
            //                    if (runOnSave && runOnSave.value === '1') {
            //                        const userGroupOptInOut = settings.find(x => x.alias === 'userGroupOptInOut');
            //                        checkGroup(userGroupOptInOut);
            //                    }
            //                });
            //        }]);
            //    }

            //    return request || $q.when(request);
            //},
            response: response => {
                try {
                    if (response.config.url.toLowerCase().indexOf(postSaveUrl) !== -1) {

                        const index = notificationsService.current.map(c => c.key === 'preflight_notice')
                            .indexOf(true);

                        if (index !== -1) {
                            notificationsService.remove(index);
                        }

                        if (response.data.notifications) {
                            
                            const notification = response.data.notifications.filter(f => f.header === Umbraco.Sys.ServerVariables.Preflight.ContentFailedChecks)[0];

                            if (notification) {
                                response.data.notifications = [];

                                notificationsService.add({
                                    view: `${Umbraco.Sys.ServerVariables.Preflight.PluginPath}/views/failed.notification.html`,
                                    args: { saveCancelled: notification.message.indexOf('_true') !== -1 }
                                });
                            }
                        }
                    }
                }
                catch (err) {
                    console.log(err.message);
                }

                return response || $q.when(response);
            }
        };
    }

    angular.module('preflight')
        .factory('preflight.save.interceptor', ['notificationsService', '$q', '$injector', interceptor])
        .config($httpProvider => $httpProvider.interceptors.push('preflight.save.interceptor'));

})();