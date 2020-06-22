(() => {

    function ctrl($scope, languageResource, notificationsService, preflightService) {

        this.isVariant = false;

        const watchTestableProperties = () => {
            let propertiesToModify = this.settings.filter(x => x.alias.indexOf('PropertiesToTest') !== -1 && x.alias !== 'propertiesToTest');
            $scope.$watch(() => this.settings.find(x => x.alias === 'propertiesToTest').value, newVal => {
                if (newVal) {
                    for (let prop of propertiesToModify) {
                        // use the prop alias to find the checkbox set
                        for (let checkbox of document.querySelectorAll(`umb-checkbox[name*="${prop.alias}"]`)) {
                            checkbox.querySelector('.umb-form-check').classList[newVal.indexOf(checkbox.getAttribute('value')) === -1 ? 'add' : 'remove']('pf-disabled');
                        }
                    }
                }
            }, true);
        };

        languageResource.getAll()
            .then(resp => {
                this.isVariant = resp.length > 1;
                this.variants = resp;

                if (!this.isVariant) {
                    this.currentVariant = this.variants[0];
                    this.getVariantSettings();
                }
            });

        this.getVariantSettings = () => {
            this.loading = true;

            preflightService.getSettings(this.currentVariant.culture)
                .then(resp => {
                    this.settings = resp.data.settings;
                    this.tabs = resp.data.tabs;

                    this.settings.forEach(v => {
                        if (v.view.indexOf('slider') !== -1) {
                            v.config = {
                                handle: 'round',
                                initVal1: v.alias === 'longWordSyllables' ? 5 : 65,
                                maxVal: v.alias === 'longWordSyllables' ? 10 : 100,
                                minVal: 0,
                                orientation: 'horizontal',
                                step: 1,
                                tooltip: 'always',
                                tooltipPosition: 'bottom',
                            };
                        } else if (v.view.indexOf('multipletextbox') !== -1) {

                            v.value = v.value.split(',').map(val => {
                                return { value: val };
                            }).sort((a, b) => a < b);

                            v.config = {
                                min: 0,
                                max: 0
                            };

                            v.validation = {};
                        } else if (v.view.indexOf('checkboxlist') !== -1) {

                            v.value = v.value.split(',');

                            v.config = {
                                items: v.prevalues
                            };
                        }
                    });

                    this.loading = false;

                    watchTestableProperties();
                });
        }


        /**
         * 
         */
        this.saveSettings = () => {

            const min = parseInt(this.settings.filter(x => x.alias === 'readabilityTargetMinimum')[0].value);
            const max = parseInt(this.settings.filter(x => x.alias === 'readabilityTargetMaximum')[0].value);

            if (min < max) {

                if (min + 10 > max) {
                    notificationsService.warning('WARNING', 'Readability range is narrow');
                }

                // need to transform multitextbox values without digest
                // so must be a new object, not a reference
                const settingsToSave = JSON.parse(JSON.stringify(this.settings));

                settingsToSave.forEach(v => {
                    if (v.view.indexOf('multipletextbox') !== -1) {
                        v.value = v.value.map(o => o.value).join(',');
                    } else if (v.view.indexOf('checkboxlist') !== -1) {
                        v.value = v.value.join(',');
                    }
                });

                preflightService.saveSettings(settingsToSave, this.tabs, this.currentVariant.culture)
                    .then(resp => {
                        resp.data
                            ? notificationsService.success('SUCCESS', 'Settings updated')
                            : notificationsService.error('ERROR', 'Unable to save settings');

                        // reset dashboard form state
                        var formScope = angular.element(document.querySelector('[name="dashboardForm"]')).scope();
                        formScope.dashboardForm.$setPristine();
                    });
            }
            else {
                notificationsService.error('ERROR',
                    'Unable to save settings - readability minimum cannot be greater than readability maximum');
            }
        };
    }

    angular.module('preflight').controller('preflight.settings.controller', ['$scope', 'languageResource', 'notificationsService', 'preflightService', ctrl]);

})();