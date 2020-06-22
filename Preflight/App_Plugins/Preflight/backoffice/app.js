(() => {

    angular.module('preflight.components', []);
    angular.module('preflight.services', []);
     
    angular.module('preflight', [
        'preflight.components',  
        'preflight.services' 
    ]); 

    //        .config(['$provide', $provide => {
    //    $provide.decorator("$rootScope", function ($delegate) {
    //        var Scope = $delegate.constructor;
    //        var origBroadcast = Scope.prototype.$broadcast;
    //        var origEmit = Scope.prototype.$emit;

    //        Scope.prototype.$broadcast = function () {
    //            console.log("$broadcast was called on $scope " + Scope.$id + " with arguments:", arguments);
    //            return origBroadcast.apply(this, arguments);
    //        };
    //        Scope.prototype.$emit = function () {
    //            console.log("$emit was called on $scope " + Scope.$id + " with arguments:", arguments);
    //            return origEmit.apply(this, arguments);
    //        };
    //        return $delegate;
    //    });
    //}]);

    angular.module('umbraco').requires.push('preflight');

})();