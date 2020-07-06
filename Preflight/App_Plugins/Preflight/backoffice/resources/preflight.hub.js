(() => {

    function preflightHub($rootScope, $q, assetsService) {

        const scripts = [
            '/umbraco/lib/signalr/jquery.signalr.js',
            '/umbraco/backoffice/signalr/hubs'
        ];

        let starting = false;
        let callbacks = [];

        function initHub(callback) {
            callbacks.push(callback);
            if (!starting) {
                if ($.connection === undefined) {
                    starting = true;

                    const promises = [];
                    scripts.forEach(script =>
                        promises.push(assetsService.loadJs(script)));

                    $q.all(promises)
                        .then(() => processCallbacks());

                } else {
                    processCallbacks();
                    starting = false;
                }
            }
        }

        function processCallbacks() {
            while (callbacks.length) {
                const cb = callbacks.pop();
                setupHub(cb);
            }
        }

        function stopHub() {
            starting = false;
            callbacks = [];
            $.connection.hub.stop(true, true);
        }

        function setupHub(callback) {

            const proxy = $.connection.preflightHub;

            const hub = {
                start: callback => {
                    if ($.connection.hub.state !== $.connection.connectionState.disconnected) {
                        $.connection.hub.stop(true, true);
                    }
                    $.connection.hub.start().done(() => {
                        if (callback) {
                            callback();
                        }
                    })
                },
                on: (eventName, callback) => {
                    proxy.on(eventName, 
                        result => {
                            $rootScope.$apply(() => {
                                if (callback) {
                                    callback(result);
                                }
                            });
                        });
                },
                invoke: (methodName, callback) => {
                    proxy.invoke(methodName)
                        .done(result => {
                            $rootScope.$apply(() => {
                                if (callback) {
                                    callback(result);
                                }
                            });
                        });
                }
            };

            return callback(hub);
        }

        return {
            initHub: initHub,
            stopHub: stopHub
        };
    }

    angular.module('preflight.services').factory('preflightHub', ['$rootScope', '$q', 'assetsService', preflightHub]);

})();