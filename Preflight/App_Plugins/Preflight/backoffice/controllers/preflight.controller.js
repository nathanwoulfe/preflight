﻿(() => {

    function ctrl($scope, $rootScope, $element, $timeout, editorState, preflightService, preflightHub) {

        let activeVariant = editorState.current.variants.find(x => x.active);
        const currentId = editorState.current.id;
        const currentCulture = activeVariant.language ? activeVariant.language.culture : Umbraco.Sys.ServerVariables.Preflight.defaultCulture;

        let suppressDirtyCheck;

        let validPropTypes = [];
        let dirtyHashes = {};
        let propsBeingChecked = [];
        let propertiesToTrack = [];
        let dirtyProps = [];

        this.results = {
            properties: []
        };

        this.noTests = false;
        this.percentageDone = 20;
        this.progressStep = 0;

        $scope.model.badge = {
            type: 'info',
        };

        $scope.$on('$destroy', () => {
            preflightHub.stopHub();
            showPreflight();
            appTabChange();
             
            $scope.model.badge = {
                type: 'info',
            };
        });

        this.retest = () => {
            this.done = false;
            this.results = {
                properties: []
            };
            this.percentageFailed = 0;

            setBadgeCount(true);

            preflightService.check(currentId, currentCulture)
                .then(resp => validateCheckResponse(resp));
        } 


        /*
         * 
         */
        const appTabChange = $rootScope.$on('app.tabChange', (e, data) => {
            if (data.alias === 'preflight') {
                // collapse open nc controls, timeouts prevent $apply errors
                for (let openNc of document.querySelectorAll('.umb-nested-content__item--active .umb-nested-content__header-bar')) {
                    $timeout(() => openNc.click());
                }

                if (!suppressDirtyCheck) {
                    $timeout(() => {
                        checkDirty();
                        setBadgeCount();
                    });
                }
            }
        });


        /*
         * 
         */
        const showPreflight = $rootScope.$on('showPreflight', (event, data) => {
            if (data.nodeId === currentId) {
                // needs to find app closest to current scope
                const appLink = $element.closest('form').find('[data-element="sub-view-preflight"]');

                if (appLink) {
                    suppressDirtyCheck = true;
                    appLink.click();
                }
            }
        });         


        /**
         * 
         * @param {any} arr
         */
        const joinList = arr => {
            let outStr;
            if (arr.length === 1) {
                outStr = arr[0];
            } else if (arr.length === 2) {
                outStr = arr.join(' and ');
            } else if (arr.length > 2) {
                outStr = arr.slice(0, -1).join(', ') + ', and ' + arr.slice(-1);
            }

            return outStr;
        };


        /**
         * Convert a string to a hash for storage and comparison.
         * @param {string} s - the string to hashify
         * @returns {int} the generated hash
         */
        const getHash = s => s ? s.split('').reduce((a, b) => {
            a = (a << 5) - a + b.charCodeAt(0);
            return a & a;
        }, 0) : 1;


        /**
         * Get property by alias from the current variant
         * @param {any} alias
         */
        const getProperty = alias => {
            for (let tab of activeVariant.tabs) {
                for (let prop of tab.properties) {
                    if (prop.alias === alias) {
                        return prop;
                    }
                }
            }
        };


        /**
         * 
         */
        const onComplete = () => {

            // it's possible no tests ran, in which case results won't exist
            this.noTests = this.results.properties.every(x => !x.plugins.length);
            if (this.noTests) {
                $scope.model.badge = {
                    type: 'warning'
                };
                return;
            }

            // if no tests and a message has been return, show an alert icon
            if (!this.results.properties.length && this.message) {
                $scope.model.badge = {
                    type: 'alert icon-'
                };
            }

            for (let p of this.results.properties) {
                p.disabled = p.failedCount === -1;
            }

            this.showSuccessMessage = !this.results.failed && !this.noTests;
            this.done = true;
        };


        /**
         * Is the editor param Umbraco.Grid or Umbraco.NestedContent?
         * @param {any} editor
         */
        const isJsonProperty = editor => editor === 'Umbraco.Grid' || editor === 'Umbraco.NestedContent';


        /**
         * Updates the badge in the header with the number of failed tests
         */
        const setBadgeCount = pending => {
            let badgeType = '';
            let failedCount = this.results.failedCount;
            let length = this.results.properties.length;

            if (pending || this.noTests) {
                badgeType = 'warning';
            }
            else if (!length && this.message) {
                this.disabled = true;
                badgeType = 'danger icon-';
            }
            else if (failedCount > 0) {
                $scope.model.badge.count = failedCount;
                badgeType = 'alert';
            } else if (length && failedCount === 0) {
                $scope.model.badge.count = null;
                badgeType = 'success icon-';
            }

            $scope.model.badge.type = badgeType;
        };


        /**
         * Updates the property set with the new value, and removes any temporary property from that set
         * @param {object} data - a response model item returned via the signalr hub
         */
        const rebindResult = data => {

            let newProp = true;
            let totalTestsRun = 0;
            let existingProp = this.results.properties.find(x => x.label === data.label);

            if (existingProp) {
                existingProp = Object.assign(existingProp, data);
                existingProp.loading = false;
                newProp = false;
            }

            // a new property will have a temporary placeholder - remove it
            // _temp ensures grid with multiple editors only removes the correct temp entry
            if (newProp && !data.remove && data.failedCount !== -1) {
                const tempIndex = this.results.properties.findIndex(p => p.name === `${data.name}_temp`);
                if (tempIndex !== -1) {
                    this.results.properties.splice(tempIndex, 1);
                }
                this.results.properties.push(data);
            }

            this.results.properties = this.results.properties.filter(x => x.remove === false || x.failedCount > -1);

            this.results.failedCount = this.results.properties.reduce((prev, cur) => {
                totalTestsRun += cur.totalTests;
                return prev + cur.failedCount;
            }, 0);

            this.results.failed = this.results.failedCount > 0;
            this.propsBeingCheckedStr = joinList(propsBeingChecked.splice(propsBeingChecked.indexOf(data.name), 1));
            this.percentageFailed = (totalTestsRun - this.results.failedCount) / totalTestsRun * 100;

            this.noTests = totalTestsRun === 0;
        };


        /**
         * Finds dirty content properties, checks the type and builds a collection of simple models for posting to the preflight checkdirty endpoint
         * Also generates and stores a hash of the property value for comparison on subsequent calls, to prevent re-fetching unchanged data
         */
        const checkDirty = () => {

            if (this.disabled)
                return;

            dirtyProps = [];
            let hasDirty = false;

            for (let prop of propertiesToTrack) {
                let currentValue = getProperty(prop.alias).value;
                currentValue = isJsonProperty(prop.editor) ? JSON.stringify(currentValue) : currentValue;

                const hash = getHash(currentValue);

                if (dirtyHashes[prop.label] && dirtyHashes[prop.label] !== hash) {

                    dirtyProps.push({
                        name: prop.label,
                        value: currentValue,
                        editor: prop.editor
                    });

                    dirtyHashes[prop.label] = hash;
                    hasDirty = true;
                } else if (!dirtyHashes[prop.label]) {
                    dirtyHashes[prop.label] = hash;
                }
            }

            // if dirty properties exist, create a simple model for each and send the lot off for checking
            // response comes via the signalr hub so is not handled here
            if (hasDirty) {
                $timeout(() => {

                    dirtyProps.forEach(prop => {
                        for (let existing of this.results.properties.filter(p => p.name === prop.name)) {
                            if (existing) {
                                existing.open = false;
                                existing.failedCount = -1;
                            } else {
                                // generate new placeholder for pending results - this is removed when the response is returned
                                this.results.properties.push({
                                    label: prop.name,
                                    open: false,
                                    failed: false,
                                    failedCount: -1,
                                    name: `${prop.name}_temp`
                                });
                            }
                        }

                        propsBeingChecked.push(prop.name);
                    });

                    this.propsBeingCheckedStr = joinList(propsBeingChecked);

                    const payload = {
                        properties: dirtyProps,
                        nodeId: currentId,
                        culture: currentCulture
                    };

                    setBadgeCount(true);
                    this.done = false;                    

                    preflightService.checkDirty(payload);
                });
            }
        };


        /**
         * Manage cases where the check fails - ie settings do not exist for the current variant 
         * @param {any} data
         */
        const validateCheckResponse = data => {
            if (data.message && data.status === 200) {
                this.message = data.message;
            }
        };


        /**
         * Initiates the signalr hub for returning test results
         */
        const startHub = () => {
            preflightHub.initHub(hub => {

                hub.on('preflightTest',
                    e => {
                        if (e.culture === currentCulture) {
                            rebindResult(e);
                            setBadgeCount();
                            this.done = false;
                        }
                    });

                hub.on('preflightComplete', () => onComplete());
                
                hub.start(() => doInitialPreflight());
            });
        };


        /**
        * Check all properties when the controller loads. Won't re-run when changing between apps
        * but needs to happen after the hub loads or on any variant change (tracked by route update)
        */
        const doInitialPreflight = () => {
            $timeout(() => {
                setBadgeCount(true);
                checkDirty(); // builds initial hash array, but won't run anything
                preflightService.check(currentId, currentCulture)
                    .then(resp => validateCheckResponse(resp));
            });
        }


        /**
         * Stores a reference collection of tracked properties
         */
        const collectProperties = () => {
            if (activeVariant) {
                activeVariant.tabs.forEach(x => {
                    propertiesToTrack = propertiesToTrack.concat(x.properties.map(x => {
                        if (validPropTypes.includes(x.editor)) {
                            return {
                                editor: x.editor,
                                alias: x.alias,
                                label: x.label
                            };
                        }
                    })).filter(x => x);
                });

                startHub();
            }
        }

        // find out what to actually test - no point watch lots of properties if they're excluded from testing...
        preflightService.getPropertiesForCurrent(currentCulture, editorState.current.contentTypeAlias)
            .then(resp => {
                validPropTypes = resp.properties || [];
                collectProperties();
            });
    }

    angular.module('preflight').controller('preflight.controller', ['$scope', '$rootScope', '$element', '$timeout', 'editorState', 'preflightService', 'preflightHub', ctrl]);

})();